using System;
using System.IO;
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
/// Monitorea la carpeta física de digitalización y dispara la ingesta y extracción OCR.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _inboxPath;
    private readonly string _processedPath;
    private readonly int _pollingIntervalSeconds;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        var basePath = configuration["ScanFolder:Path"]
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScanStation");

        _inboxPath = Path.Combine(basePath, "Inbox");
        _processedPath = Path.Combine(basePath, "Processed");

        _pollingIntervalSeconds = int.TryParse(configuration["ScanFolder:IntervalSeconds"], out var sec) ? sec : 5;

        Directory.CreateDirectory(_inboxPath);
        Directory.CreateDirectory(_processedPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🖥️ Shohin Agente On-Premise iniciado.");
        _logger.LogInformation("Vigilando carpeta de escaneo: {Path}", _inboxPath);

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
        var archivos = Directory.GetFiles(_inboxPath, "*.pdf");
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

            var nombreArchivo = Path.GetFileName(archivoRuta);
            _logger.LogInformation("📤 Ingestando y transfiriendo a Azure Blob Storage: {Nombre}", nombreArchivo);

            try
            {
                using (var stream = File.OpenRead(archivoRuta))
                {
                    // Se sube a Azure Blob Storage y se registra encolado. NO ejecuta OCR localmente.
                    // La creación del blob en la nube engatillará el evento para que la Azure Function ejecute el OCR en la nube.
                    var docResult = await digitalizacionService.IngestarDocumentoPendienteAsync(idTicket, stream, nombreArchivo);
                    if (docResult.Exito)
                    {
                        _logger.LogInformation("☁️ Documento {Nombre} subido a la nube. Estado: {Estado}. Esperando engatillado por evento en Azure.", nombreArchivo, docResult.Datos?.Estado);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Error al transferir documento {Nombre}: {Msg}", nombreArchivo, docResult.Mensaje);
                    }
                }

                // Mover a carpeta de procesados
                var destino = Path.Combine(_processedPath, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{nombreArchivo}");
                File.Move(archivoRuta, destino, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al procesar el archivo {Nombre}", nombreArchivo);
            }
        }
    }
}
