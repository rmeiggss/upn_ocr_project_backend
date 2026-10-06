using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shohin.Application.DTOs.Tickets;
using Shohin.Application.Interfaces;
using Shohin.Application.Services;
using Shohin.Domain.Enums;

namespace Shohin.Worker.Local;

/// <summary>
/// Agente On-Premise en estación de escaneo (Shohin S.A.).
/// 1. Sincroniza y auto-aprovisiona carpetas locales para tickets en estado PENDIENTE.
/// 2. Monitorea las carpetas por Ticket y transfiere documentos a Azure Blob Storage.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly List<string> _inboxPaths = new();
    private readonly List<string> _processedPaths = new();
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
                if (!_processedPaths.Contains(processed))
                {
                    _processedPaths.Add(processed);
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
            _logger.LogInformation("📂 Vigilando estación de escaneo: {Path}", p);
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

    /// <summary>
    /// Sincroniza la estación de escaneo para que sea un espejo fiel en tiempo real de la nube:
    /// 1. Elimina de Inbox y Processed cualquier carpeta de ticket que haya sido borrado en la nube.
    /// 2. Si un ticket cambió de estado y ya no es PENDIENTE (ej. PROCESADO, OBSERVADO): retira su carpeta de Inbox.
    /// 3. Si un ticket está PENDIENTE en la nube: asegura que su carpeta y _INFO_TICKET.txt existan en Inbox.
    /// </summary>
    private async Task SincronizarEspejoConNubeAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            // 1. Obtener la lista completa de tickets existentes en la nube con su estado y relaciones
            var ticketsEnNube = await dbContext.Tickets
                .AsNoTracking()
                .Include(t => t.EstadoParametro)
                .Include(t => t.PrioridadParametro)
                .Include(t => t.TipoDocumentoParametro)
                .ToListAsync(stoppingToken);

            var cloudTicketsMap = ticketsEnNube
                .Where(t => !string.IsNullOrWhiteSpace(t.CodigoTicket))
                .ToDictionary(t => t.CodigoTicket.Trim(), t => t, StringComparer.OrdinalIgnoreCase);

            var ticketsPendientes = ticketsEnNube
                .Where(t => t.EstadoParametro != null &&
                            string.Equals(t.EstadoParametro.Clave, ParametroConstantes.EstadoTicket.Pendiente, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 2. Reconciliación espejo en bandejas Inbox (Eliminación por borrado y cambio de estado)
            foreach (var inbox in _inboxPaths.Where(Directory.Exists))
            {
                var subDirectorios = Directory.GetDirectories(inbox);
                foreach (var ticketDir in subDirectorios)
                {
                    var folderName = Path.GetFileName(ticketDir);
                    if (string.IsNullOrWhiteSpace(folderName)) continue;

                    // CASO A: EL TICKET FUE BORRADO EN LA NUBE (Ya no existe en la base de datos)
                    if (!cloudTicketsMap.TryGetValue(folderName, out var ticketEnNube))
                    {
                        try
                        {
                            Directory.Delete(ticketDir, true);
                            _logger.LogInformation("🗑️ [Espejo Nube] Ticket {Ticket} ya no existe en la nube (fue eliminado). Eliminada carpeta local en Inbox: {Path}", folderName, ticketDir);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("No se pudo eliminar carpeta huérfana en Inbox {Path}: {Msg}", ticketDir, ex.Message);
                        }
                        continue;
                    }

                    // CASO B: EL TICKET EXISTE, PERO SU ESTADO CAMBIÓ Y YA NO ES PENDIENTE
                    var estadoClave = ticketEnNube.EstadoParametro?.Clave?.ToUpperInvariant() ?? "";
                    if (estadoClave != ParametroConstantes.EstadoTicket.Pendiente)
                    {
                        try
                        {
                            var pdfsEnCarpeta = Directory.GetFiles(ticketDir, "*.pdf");
                            if (estadoClave == ParametroConstantes.EstadoTicket.Procesado && pdfsEnCarpeta.Length > 0)
                            {
                                var baseFolder = inbox.Contains("Inbox") ? inbox[..inbox.IndexOf("Inbox")] : Path.GetDirectoryName(inbox) ?? inbox;
                                var processedDir = Path.Combine(baseFolder, "Processed", folderName);
                                Directory.CreateDirectory(processedDir);
                                foreach (var pdf in pdfsEnCarpeta)
                                {
                                    var destPdf = Path.Combine(processedDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Path.GetFileName(pdf)}");
                                    File.Move(pdf, destPdf, true);
                                }
                            }

                            // Limpiar ficha informativa y retirar carpeta de Inbox
                            var infoFile = Path.Combine(ticketDir, "_INFO_TICKET.txt");
                            if (File.Exists(infoFile)) File.Delete(infoFile);

                            var restantes = Directory.GetFiles(ticketDir);
                            if (restantes.Length == 0)
                            {
                                Directory.Delete(ticketDir, true);
                                _logger.LogInformation("🔄 [Espejo Nube] Ticket {Ticket} cambió a estado '{Estado}'. Retirada carpeta de Inbox de escaneo.", folderName, estadoClave);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ Ticket {Ticket} cambió a estado '{Estado}', pero contiene {Count} archivo(s) no procesados.", folderName, estadoClave, restantes.Length);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Error al retirar carpeta de Inbox para Ticket {Ticket} con estado {Estado}: {Msg}", folderName, estadoClave, ex.Message);
                        }
                    }
                }

                // CASO C: TICKETS EN ESTADO PENDIENTE EN LA NUBE -> Asegurar auto-aprovisionamiento en Inbox
                foreach (var ticket in ticketsPendientes)
                {
                    if (string.IsNullOrWhiteSpace(ticket.CodigoTicket)) continue;

                    var ticketDir = Path.Combine(inbox, ticket.CodigoTicket);
                    if (!Directory.Exists(ticketDir))
                    {
                        Directory.CreateDirectory(ticketDir);
                        _logger.LogInformation("📁 [Auto-Provisioning] Creada carpeta para Ticket {Ticket} en Inbox: {Path}", ticket.CodigoTicket, ticketDir);
                    }

                    var infoPath = Path.Combine(ticketDir, "_INFO_TICKET.txt");
                    await EscribirFichaInformativaAsync(infoPath, ticket, stoppingToken);
                }
            }

            // 3. Reconciliación espejo en carpetas Processed (Eliminar si el ticket fue borrado en la nube)
            foreach (var processed in _processedPaths.Where(Directory.Exists))
            {
                var carpetasProcesadas = Directory.GetDirectories(processed);
                foreach (var procDir in carpetasProcesadas)
                {
                    var folderName = Path.GetFileName(procDir);
                    if (string.IsNullOrWhiteSpace(folderName)) continue;

                    if ((folderName.StartsWith("TK-", StringComparison.OrdinalIgnoreCase) || folderName.StartsWith("TCK-", StringComparison.OrdinalIgnoreCase))
                        && !cloudTicketsMap.ContainsKey(folderName))
                    {
                        try
                        {
                            Directory.Delete(procDir, true);
                            _logger.LogInformation("🗑️ [Espejo Nube] Ticket {Ticket} fue eliminado en la nube. Eliminada carpeta archivada en Processed: {Path}", folderName, procDir);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("No se pudo eliminar carpeta en Processed para ticket eliminado {Ticket}: {Msg}", folderName, ex.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ [Sincronización Espejo] No se pudo verificar el estado en la nube ({Msg}). Reintentando en el próximo ciclo...", ex.Message);
        }
    }

    private static async Task EscribirFichaInformativaAsync(string infoPath, Shohin.Domain.Entities.TicketDigitalizacion ticket, CancellationToken stoppingToken)
    {
        var ruc = !string.IsNullOrWhiteSpace(ticket.RucProveedor) ? ticket.RucProveedor : "N/A";
        var razon = !string.IsNullOrWhiteSpace(ticket.RazonSocialProveedor) ? ticket.RazonSocialProveedor : "N/A";
        var tipoDoc = ticket.TipoDocumentoParametro != null ? ticket.TipoDocumentoParametro.Clave : "FACTURA";
        var rango = (ticket.FechaDesde.HasValue && ticket.FechaHasta.HasValue)
            ? $"{ticket.FechaDesde.Value:yyyy-MM-dd} al {ticket.FechaHasta.Value:yyyy-MM-dd}"
            : (ticket.FechaDesde.HasValue ? $"Desde {ticket.FechaDesde.Value:yyyy-MM-dd}" : "Período no especificado");
        var caja = !string.IsNullOrWhiteSpace(ticket.NumeroCajaArchivador) ? ticket.NumeroCajaArchivador : "Caja regular";
        var prioridad = ticket.PrioridadParametro != null ? ticket.PrioridadParametro.Clave : "ALTA";
        var obs = !string.IsNullOrWhiteSpace(ticket.Observaciones) ? ticket.Observaciones : "Lote asignado para digitalización y conciliación";

        var infoContent =
$@"======================================================================
SISTEMA DE DIGITALIZACIÓN Y OCR - SHOHIN S.A.
TICKET DE LOTE ASIGNADO A PERSONAL DE ARCHIVO (CUS-01)
======================================================================
Código de Ticket     : {ticket.CodigoTicket}
Estado Inicial       : PENDIENTE
Folios Esperados     : {ticket.TotalDocumentosEsperados}
Proveedor (RUC)      : {ruc}
Razón Social         : {razon}
Tipo de Comprobante  : {tipoDoc}
Rango / Período      : {rango}
Caja / Archivador    : {caja}
Prioridad            : {prioridad}
Observaciones/Motivo : {obs}
Fecha de Emisión     : {ticket.FechaCreacion:yyyy-MM-dd HH:mm:ss}

INSTRUCCIONES PARA EL PERSONAL DE ARCHIVO:
1. Ubique en el almacén físico la caja o archivador indicado arriba.
2. Cuente físicamente los folios y verifique contra los folios esperados.
3. Escanee los comprobantes físicos (.pdf) directamente en esta carpeta.
4. El Agente On-Premise detectará automáticamente los PDFs, los asociará
   a este ticket y los transferirá a Azure Blob Storage para el OCR.
======================================================================";

        await File.WriteAllTextAsync(infoPath, infoContent, stoppingToken);
    }

    private async Task ProcesarArchivosPendientesAsync(CancellationToken stoppingToken)
    {
        // 1. Sincronización espejo con la nube (borrados, cambios de estado y aprovisionamiento)
        await SincronizarEspejoConNubeAsync(stoppingToken);

        foreach (var inbox in _inboxPaths.Where(Directory.Exists))
        {
            // 2. Procesar subcarpetas que correspondan a Tickets
            var subDirectorios = Directory.GetDirectories(inbox);
            foreach (var ticketDir in subDirectorios)
            {
                if (stoppingToken.IsCancellationRequested) break;

                var folderName = Path.GetFileName(ticketDir);
                var pdfsEnCarpeta = Directory.GetFiles(ticketDir, "*.pdf");

                if (pdfsEnCarpeta.Length == 0) continue;

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var digitalizacionService = scope.ServiceProvider.GetRequiredService<DigitalizacionService>();

                var ticket = await dbContext.Tickets
                    .Include(t => t.EstadoParametro)
                    .FirstOrDefaultAsync(t => t.CodigoTicket == folderName, stoppingToken);

                int targetTicketId;
                string targetCodigoTicket;

                if (ticket != null)
                {
                    targetTicketId = ticket.IdTicket;
                    targetCodigoTicket = ticket.CodigoTicket;
                    _logger.LogInformation("📂 Detectados {Count} PDF(s) en carpeta del Ticket {Ticket} (ID: {Id}).",
                        pdfsEnCarpeta.Length, targetCodigoTicket, targetTicketId);
                }
                else
                {
                    // Si el nombre tiene formato de ticket formal (TK- o TCK-) pero no existe en BD,
                    // significa que fue eliminado deliberadamente en la nube. NO recrearlo.
                    if (folderName.StartsWith("TK-", StringComparison.OrdinalIgnoreCase) || folderName.StartsWith("TCK-", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("⚠️ Carpeta {Folder} tiene código de ticket borrado en la nube. Omitiendo recreación automática.", folderName);
                        continue;
                    }

                    // Carpeta de escaneo manual arbitraria: crear ticket automático
                    var resp = await digitalizacionService.CrearTicketAsync(new CrearTicketDto
                    {
                        CodigoTicket = null,
                        TotalDocumentosEsperados = pdfsEnCarpeta.Length,
                        Observaciones = $"Lote creado automáticamente desde carpeta local [{folderName}] ({DateTime.UtcNow:yyyy-MM-dd HH:mm})"
                    });

                    if (!resp.Exito || resp.Datos == null) continue;
                    targetTicketId = resp.Datos.IdTicket;
                    targetCodigoTicket = resp.Datos.CodigoTicket;
                }

                // Ingestar cada PDF de la subcarpeta asociándolo al ticket
                foreach (var pdfPath in pdfsEnCarpeta)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await IngestarArchivoAsync(digitalizacionService, targetTicketId, targetCodigoTicket, pdfPath, stoppingToken);
                }

                // Si ya no quedan PDFs en la carpeta, archivar el archivo _INFO_TICKET y limpiar de Inbox
                var restantes = Directory.GetFiles(ticketDir, "*.pdf");
                if (restantes.Length == 0)
                {
                    try
                    {
                        var infoFile = Path.Combine(ticketDir, "_INFO_TICKET.txt");
                        if (File.Exists(infoFile)) File.Delete(infoFile);
                        Directory.Delete(ticketDir, false);
                        _logger.LogInformation("🧹 Carpeta {Ticket} completada y liberada de Inbox.", folderName);
                    }
                    catch { }
                }
            }

            // 3. Procesar archivos PDF sueltos en la raíz de Inbox (Fallback)
            var archivosSueltos = Directory.GetFiles(inbox, "*.pdf");
            if (archivosSueltos.Length > 0)
            {
                _logger.LogInformation("Detectados {Count} archivo(s) PDF sueltos en raíz de Inbox.", archivosSueltos.Length);
                using var scope = _serviceProvider.CreateScope();
                var digitalizacionService = scope.ServiceProvider.GetRequiredService<DigitalizacionService>();

                var ticketResp = await digitalizacionService.CrearTicketAsync(new CrearTicketDto
                {
                    TotalDocumentosEsperados = archivosSueltos.Length,
                    Observaciones = $"Lote automático generado por Estación de Escaneo Local ({DateTime.UtcNow:yyyy-MM-dd HH:mm})"
                });

                if (ticketResp.Exito && ticketResp.Datos != null)
                {
                    foreach (var archivoRuta in archivosSueltos)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        await IngestarArchivoAsync(digitalizacionService, ticketResp.Datos.IdTicket, ticketResp.Datos.CodigoTicket, archivoRuta, stoppingToken);
                    }
                }
            }
        }
    }

    private async Task IngestarArchivoAsync(
        DigitalizacionService digitalizacionService,
        int idTicket,
        string codigoTicket,
        string archivoRuta,
        CancellationToken stoppingToken)
    {
        var nombreArchivo = Path.GetFileName(archivoRuta);
        _logger.LogInformation("📤 Ingestando y transfiriendo a Azure Blob Storage: {Nombre} para Ticket {Ticket}", nombreArchivo, codigoTicket);

        try
        {
            string? rutaBlob = null;
            using (var stream = File.OpenRead(archivoRuta))
            {
                var docResult = await digitalizacionService.IngestarDocumentoPendienteAsync(idTicket, stream, nombreArchivo);
                if (docResult.Exito)
                {
                    rutaBlob = docResult.Datos?.RutaBlobStorage;
                    _logger.LogInformation("☁️ Documento {Nombre} subido a la nube. Estado: {Estado}. Esperando OCR.", nombreArchivo, docResult.Datos?.Estado);
                }
                else
                {
                    _logger.LogWarning("⚠️ Error al transferir documento {Nombre}: {Msg}", nombreArchivo, docResult.Mensaje);
                }
            }

            // Mover archivo físico procesado a Processed/{CodigoTicket}/
            var inboxFolder = Path.GetDirectoryName(archivoRuta) ?? _inboxPaths[0];
            var baseFolder = inboxFolder.Contains("Inbox") ? inboxFolder[..inboxFolder.IndexOf("Inbox")] : Path.GetDirectoryName(inboxFolder) ?? inboxFolder;
            var processedDir = Path.Combine(baseFolder, "Processed", codigoTicket);
            Directory.CreateDirectory(processedDir);
            var destino = Path.Combine(processedDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{nombreArchivo}");
            File.Move(archivoRuta, destino, true);
            _logger.LogInformation("📁 Archivo físico movido a: {Destino}", destino);

            // Simulación de evento OCR si está en modo Local
            if (_configuration["BlobStorage:Provider"] == "Local" && !string.IsNullOrEmpty(rutaBlob))
            {
                var blobParaOcr = rutaBlob;
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
                            _logger.LogInformation("✅ [Simulación OCR] OCR completado para {Nombre}. Estado final: {Estado}", nombreArchivo, procResult.Datos?.Estado);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error en simulación local de OCR.");
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
