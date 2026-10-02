using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shohin.Application.DTOs.Tickets;
using Shohin.Application.Services;

namespace Shohin.Worker.Local;

/// <summary>
/// Agente On-Premise en estación de escaneo (Shohin S.A.).
/// Monitorea la carpeta física de digitalización y transfiere documentos a Azure Blob Storage.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly List<string> _inboxPaths = new();
    private readonly int _pollingIntervalSeconds;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;

        // Registrar carpetas candidatas para evitar problemas de ruta relativa según desde dónde se lance dotnet run
        var pathsToTry = new[]
        {
            Path.GetFullPath(configuration["ScanFolder:Path"] ?? "ScanStation"),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "ScanStation")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScanStation")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "ScanStation")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "ScanStation"))
        };

        foreach (var basePath in pathsToTry.Distinct())
        {
            var inbox = Path.Combine(basePath, "Inbox");
            var processed = Path.Combine(basePath, "Processed");
            try
            {
                Directory.CreateDirectory(inbox);
                Directory.CreateDirectory(processed);
                if (!_inboxPaths.Contains(inbox))
                {
                    _inboxPaths.Add(inbox);
                }
            }
            catch { }
        }

        _pollingIntervalSeconds = int.TryParse(configuration["ScanFolder:IntervalSeconds"], out var sec) ? sec : 5;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🖥️ Shohin Agente On-Premise iniciado.");
        foreach (var p in _inboxPaths)
        {
            _logger.LogInformation("📂 Vigilando carpeta de escaneo: {Path}", p);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarArchivosPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la iteración del agente de escaneo.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Deteniendo agente local de escaneo.");
    }

    private async Task ProcesarArchivosPendientesAsync(CancellationToken stoppingToken)
    {
        var archivos = _inboxPaths
            .Where(Directory.Exists)
            .SelectMany(p => Directory.GetFiles(p, "*.pdf"))
            .Distinct()
            .ToArray();

        if (archivos.Length == 0) return;

        _logger.LogInformation("Detectados {Count} archivo(s) PDF para digitalización.", archivos.Length);

        using var scope = _serviceProvider.CreateScope();
        var digitalizacionService = scope.ServiceProvider.GetRequiredService<DigitalizacionService>();

        // Crear un lote/ticket para el paquete de documentos detectados
        var ticketResp = await digitalizacionService.CrearTicketAsync(new CrearTicketDto
        {
            TotalDocumentosEsperados = archivos.Length,
            Observaciones = $"Lote automático generado por Estación de Escaneo Local ({DateTime.UtcNow:yyyy-MM-dd HH:mm})"
        });

        if (!ticketResp.Exito || ticketResp.Datos == null)
        {
            _logger.LogError("No se pudo inicializar el ticket para el lote: {Msg}", ticketResp.Mensaje);
            return;
        }

        var idTicket = ticketResp.Datos.IdTicket;
        _logger.LogInformation("Ticket {Codigo} generado exitosamente (ID: {Id}).", ticketResp.Datos.CodigoTicket, idTicket);

        foreach (var archivoRuta in archivos)
        {
            if (stoppingToken.IsCancellationRequested) break;
            if (!File.Exists(archivoRuta)) continue;

            var nombreArchivo = Path.GetFileName(archivoRuta);
            _logger.LogInformation("📤 Ingestando y transfiriendo a Azure Blob Storage: {Nombre}", nombreArchivo);

            try
            {
                string? rutaBlob = null;
                using (var stream = File.OpenRead(archivoRuta))
                {
                    // Se sube a Azure Blob Storage y se registra encolado. NO ejecuta OCR pesado en la máquina local.
                    // La creación del blob en la nube engatillará el evento para que la Azure Function ejecute el OCR en la nube.
                    var docResult = await digitalizacionService.IngestarDocumentoPendienteAsync(idTicket, stream, nombreArchivo);
                    if (docResult.Exito)
                    {
                        rutaBlob = docResult.Datos?.RutaBlobStorage;
                        _logger.LogInformation("☁️ Documento {Nombre} subido a la nube. Estado: {Estado}. Esperando engatillado por evento en Azure.", nombreArchivo, docResult.Datos?.Estado);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Error al transferir documento {Nombre}: {Msg}", nombreArchivo, docResult.Mensaje);
                    }
                }

                // Mover a carpeta de procesados correspondiente
                var inboxFolder = Path.GetDirectoryName(archivoRuta) ?? _inboxPaths[0];
                var baseFolder = Path.GetDirectoryName(inboxFolder) ?? inboxFolder;
                var processedDir = Path.Combine(baseFolder, "Processed");
                Directory.CreateDirectory(processedDir);
                var destino = Path.Combine(processedDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{nombreArchivo}");
                File.Move(archivoRuta, destino, true);
                _logger.LogInformation("📁 Archivo físico movido a: {Destino}", destino);

                // En modo local (sin Azure Event Grid real activo), simulamos el disparo del evento de nube
                if (_configuration["BlobStorage:Provider"] == "Local" && !string.IsNullOrEmpty(rutaBlob))
                {
                    var blobParaOcr = rutaBlob;
                    _logger.LogInformation("⚡ [Simulación Event Grid Cloud] Emulando evento 'BlobCreated' -> Cola OCR...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(1200, stoppingToken);
                            using var ocrScope = _serviceProvider.CreateScope();
                            var svc = ocrScope.ServiceProvider.GetRequiredService<DigitalizacionService>();
                            var procResult = await svc.ProcesarDocumentoPorEventoOcrAsync(blobParaOcr);
                            if (procResult.Exito)
                            {
                                _logger.LogInformation("✅ [Simulación Event Grid Cloud] OCR completado para {Nombre}. Estado final: {Estado} (Total: {Total} {Moneda})",
                                    nombreArchivo, procResult.Datos?.Estado, procResult.Datos?.MontoTotal, procResult.Datos?.Moneda);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [Simulación Event Grid Cloud] Error en OCR: {Msg}", procResult.Mensaje);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error en simulación local de evento OCR.");
                        }
                    }, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al procesar el archivo {Nombre}", nombreArchivo);
            }
        }
    }
}
