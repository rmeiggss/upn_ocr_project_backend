using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Documentos;
using Shohin.Application.DTOs.Tickets;
using Shohin.Application.Interfaces;
using Shohin.Domain.Entities;
using Shohin.Domain.Enums;

namespace Shohin.Application.Services;

public class DigitalizacionService
{
    private readonly IApplicationDbContext _context;
    private readonly IOcrService _ocrService;
    private readonly IBlobStorageService _blobService;

    public DigitalizacionService(
        IApplicationDbContext context,
        IOcrService ocrService,
        IBlobStorageService blobService)
    {
        _context = context;
        _ocrService = ocrService;
        _blobService = blobService;
    }

    public async Task<ApiResponse<List<TicketDto>>> ObtenerTicketsAsync()
    {
        var tickets = await _context.Tickets
            .Include(t => t.EstadoParametro)
            .Include(t => t.PrioridadParametro)
            .Include(t => t.Documentos)
                .ThenInclude(d => d.EstadoParametro)
            .OrderByDescending(t => t.FechaCreacion)
            .Select(t => new TicketDto
            {
                IdTicket = t.IdTicket,
                CodigoTicket = t.CodigoTicket,
                Estado = t.EstadoParametro.Clave,
                TotalDocumentosEsperados = t.TotalDocumentosEsperados,
                TotalDocumentosProcesados = t.TotalDocumentosProcesados,
                Observaciones = t.Observaciones,
                FechaCreacion = t.FechaCreacion,
                UsuarioCreacion = t.UsuarioCreacion,
                FechaDesde = t.FechaDesde,
                FechaHasta = t.FechaHasta,
                NumeroCajaArchivador = t.NumeroCajaArchivador,
                RucProveedor = t.RucProveedor,
                RazonSocialProveedor = t.RazonSocialProveedor,
                Prioridad = t.PrioridadParametro != null ? t.PrioridadParametro.Clave : null,
                TotalObservados = t.Documentos.Count(d => d.EstadoParametro.Clave == ParametroConstantes.EstadoDocumento.Observado),
                TotalCorrectos = t.Documentos.Count(d => d.EstadoParametro.Clave == ParametroConstantes.EstadoDocumento.Correcto)
            })
            .ToListAsync();

        return ApiResponse<List<TicketDto>>.Ok(tickets);
    }

    public async Task<ApiResponse<TicketDto>> CrearTicketAsync(CrearTicketDto request)
    {
        var estadoPendiente = await _context.Parametros
            .FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Pendiente);

        var codigo = string.IsNullOrWhiteSpace(request.CodigoTicket)
            ? $"TCK-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}"
            : request.CodigoTicket;

        var ticket = new TicketDigitalizacion
        {
            CodigoTicket = codigo,
            IdEstadoParametro = estadoPendiente.IdParametro,
            TotalDocumentosEsperados = request.TotalDocumentosEsperados,
            TotalDocumentosProcesados = 0,
            Observaciones = request.Observaciones
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        return ApiResponse<TicketDto>.Ok(new TicketDto
        {
            IdTicket = ticket.IdTicket,
            CodigoTicket = ticket.CodigoTicket,
            Estado = estadoPendiente.Clave,
            TotalDocumentosEsperados = ticket.TotalDocumentosEsperados,
            TotalDocumentosProcesados = 0,
            Observaciones = ticket.Observaciones,
            FechaCreacion = ticket.FechaCreacion,
            UsuarioCreacion = ticket.UsuarioCreacion
        }, "Ticket creado exitosamente.");
    }

    public async Task<ApiResponse<DocumentoDto>> ProcesarDocumentoIndividualAsync(int idTicket, Stream archivoStream, string nombreArchivo)
    {
        var ticket = await _context.Tickets
            .Include(t => t.EstadoParametro)
            .FirstOrDefaultAsync(t => t.IdTicket == idTicket);

        if (ticket == null)
            return ApiResponse<DocumentoDto>.Fail("Ticket no encontrado.");

        // 1. Calcular hash de integridad (SHA-256)
        archivoStream.Position = 0;
        string hashIntegridad;
        using (var sha = SHA256.Create())
        {
            var hashBytes = await sha.ComputeHashAsync(archivoStream);
            hashIntegridad = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        archivoStream.Position = 0;

        // 2. Subir a Blob Storage
        var rutaBlob = await _blobService.SubirArchivoAsync(archivoStream, nombreArchivo);
        archivoStream.Position = 0;

        // 3. Ejecutar OCR (Azure Document Intelligence o Mock)
        var ocrResult = await _ocrService.ExtraerDatosDocumentoAsync(archivoStream, nombreArchivo);

        // 4. Determinar tipo de documento y estado según confianza
        var tipoDocParam = await _context.Parametros
            .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ocrResult.TipoDocumentoSugerido)
            ?? await _context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ParametroConstantes.TipoDocumento.Factura);

        var estadoDocClave = ocrResult.ConfianzaGeneral >= 85.0m
            ? ParametroConstantes.EstadoDocumento.Correcto
            : ParametroConstantes.EstadoDocumento.Observado;

        var estadoDocParam = await _context.Parametros
            .FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == estadoDocClave);

        // 5. Crear DocumentoContable
        var doc = new DocumentoContable
        {
            IdTicket = ticket.IdTicket,
            IdParametroTipoDocumento = tipoDocParam.IdParametro,
            RucEmisor = ocrResult.RucEmisor,
            RazonSocial = ocrResult.RazonSocial,
            SerieComprobante = ocrResult.SerieComprobante,
            NumeroComprobante = ocrResult.NumeroComprobante,
            FechaEmision = ocrResult.FechaEmision,
            MontoSubTotal = ocrResult.MontoSubTotal,
            MontoIgv = ocrResult.MontoIgv,
            MontoTotal = ocrResult.MontoTotal,
            Moneda = ocrResult.Moneda,
            IdEstadoParametro = estadoDocParam.IdParametro,
            RutaBlobStorage = rutaBlob,
            NombreArchivo = nombreArchivo,
            HashIntegridad = hashIntegridad
        };

        // 6. Campos OCR (Composición)
        foreach (var c in ocrResult.Campos)
        {
            doc.CamposExtraidos.Add(new CampoExtraidoOCR
            {
                NombreCampo = c.NombreCampo,
                ValorExtraido = c.Valor,
                NivelConfianza = c.NivelConfianza,
                EsCorregido = false
            });
        }

        _context.Documentos.Add(doc);

        // 7. Actualizar contador del ticket
        ticket.TotalDocumentosProcesados += 1;
        var estadoProcesado = await _context.Parametros
            .FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Procesado);
        ticket.IdEstadoParametro = estadoProcesado.IdParametro;

        await _context.SaveChangesAsync();

        return ApiResponse<DocumentoDto>.Ok(new DocumentoDto
        {
            IdDocumento = doc.IdDocumento,
            IdTicket = doc.IdTicket,
            CodigoTicket = ticket.CodigoTicket,
            TipoDocumento = tipoDocParam.Clave,
            RucEmisor = doc.RucEmisor,
            RazonSocial = doc.RazonSocial,
            SerieComprobante = doc.SerieComprobante,
            NumeroComprobante = doc.NumeroComprobante,
            FechaEmision = doc.FechaEmision,
            MontoSubTotal = doc.MontoSubTotal,
            MontoIgv = doc.MontoIgv,
            MontoTotal = doc.MontoTotal,
            Moneda = doc.Moneda,
            Estado = estadoDocParam.Clave,
            RutaBlobStorage = doc.RutaBlobStorage,
            NombreArchivo = doc.NombreArchivo,
            FechaCreacion = doc.FechaCreacion,
            Campos = doc.CamposExtraidos.Select(ce => new CampoOcrDto
            {
                IdCampoExtraido = ce.IdCampoExtraido,
                NombreCampo = ce.NombreCampo,
                ValorExtraido = ce.ValorExtraido,
                ValorCorregido = ce.ValorCorregido,
                NivelConfianza = ce.NivelConfianza,
                EsCorregido = ce.EsCorregido
            }).ToList()
        }, "Documento procesado correctamente.");
    }

    /// <summary>
    /// Ingesta rápida para agentes locales/escáner: Sube el PDF a Azure Blob Storage
    /// y crea el documento en estado PENDIENTE / PROCESANDO sin bloquear con OCR en la máquina local.
    /// La creación del blob engatillará el procesamiento asíncrono en la nube.
    /// </summary>
    public async Task<ApiResponse<DocumentoDto>> IngestarDocumentoPendienteAsync(int idTicket, Stream archivoStream, string nombreArchivo)
    {
        var ticket = await _context.Tickets
            .Include(t => t.EstadoParametro)
            .FirstOrDefaultAsync(t => t.IdTicket == idTicket);

        if (ticket == null)
            return ApiResponse<DocumentoDto>.Fail("Ticket no encontrado.");

        // 1. Calcular hash de integridad (SHA-256)
        archivoStream.Position = 0;
        string hashIntegridad;
        using (var sha = SHA256.Create())
        {
            var hashBytes = await sha.ComputeHashAsync(archivoStream);
            hashIntegridad = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        archivoStream.Position = 0;

        // 2. Subir a Blob Storage en la carpeta 'pendientes' (disparará el evento BlobCreated)
        var rutaBlob = await _blobService.SubirArchivoAsync(archivoStream, nombreArchivo, "digitalizacion/pendientes");

        // 3. Parámetros de estado inicial
        var tipoDocParam = await _context.Parametros
            .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ParametroConstantes.TipoDocumento.Factura)
            ?? await _context.Parametros.FirstAsync();

        var estadoDocParam = await _context.Parametros
            .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == ParametroConstantes.EstadoDocumento.Pendiente)
            ?? await _context.Parametros.FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == ParametroConstantes.EstadoDocumento.Observado)
            ?? await _context.Parametros.FirstAsync();

        var estadoTicketProcesando = await _context.Parametros
            .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Procesando);

        if (estadoTicketProcesando != null)
        {
            ticket.IdEstadoParametro = estadoTicketProcesando.IdParametro;
        }

        // 4. Crear registro en BD
        var doc = new DocumentoContable
        {
            IdTicket = ticket.IdTicket,
            IdParametroTipoDocumento = tipoDocParam.IdParametro,
            IdEstadoParametro = estadoDocParam.IdParametro,
            RutaBlobStorage = rutaBlob,
            NombreArchivo = nombreArchivo,
            HashIntegridad = hashIntegridad
        };

        _context.Documentos.Add(doc);
        ticket.TotalDocumentosProcesados += 1;

        await _context.SaveChangesAsync();

        return ApiResponse<DocumentoDto>.Ok(new DocumentoDto
        {
            IdDocumento = doc.IdDocumento,
            IdTicket = doc.IdTicket,
            CodigoTicket = ticket.CodigoTicket,
            TipoDocumento = tipoDocParam.Clave,
            Estado = estadoDocParam.Clave,
            RutaBlobStorage = doc.RutaBlobStorage,
            NombreArchivo = doc.NombreArchivo,
            FechaCreacion = doc.FechaCreacion
        }, "Documento subido a almacenamiento en la nube y encolado para extracción OCR.");
    }

    /// <summary>
    /// Procesador asíncrono en la nube: Engatillado por el evento de Azure Storage / QueueTrigger.
    /// Descarga el stream del blob, ejecuta OCR y actualiza campos y estado en Azure SQL.
    /// </summary>
    public async Task<ApiResponse<DocumentoDto>> ProcesarDocumentoPorEventoOcrAsync(string rutaBlobOUrl)
    {
        // Buscar documento por ruta de blob o nombre de archivo
        var doc = await _context.Documentos
            .Include(d => d.Ticket)
            .Include(d => d.CamposExtraidos)
            .FirstOrDefaultAsync(d => d.RutaBlobStorage == rutaBlobOUrl || (!string.IsNullOrEmpty(d.NombreArchivo) && rutaBlobOUrl.Contains(d.NombreArchivo)));

        if (doc == null)
        {
            // Auto-crear el registro resiliente si el documento no fue registrado previamente en la BD
            var nombreExtraido = Path.GetFileName(Uri.TryCreate(rutaBlobOUrl, UriKind.Absolute, out var u) ? u.LocalPath : rutaBlobOUrl);
            if (nombreExtraido.Contains('_'))
            {
                var partes = nombreExtraido.Split('_', 2);
                if (partes.Length == 2 && partes[0].Length == 32)
                {
                    nombreExtraido = partes[1];
                }
            }

            var ticketAuto = await _context.Tickets.FirstOrDefaultAsync(t => t.CodigoTicket == "TCK-CLOUD-EVENT");
            if (ticketAuto == null)
            {
                var estadoProcesandoParam = await _context.Parametros
                    .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Procesando)
                    ?? await _context.Parametros.FirstAsync();

                ticketAuto = new TicketDigitalizacion
                {
                    CodigoTicket = "TCK-CLOUD-EVENT",
                    TotalDocumentosEsperados = 1,
                    TotalDocumentosProcesados = 0,
                    IdEstadoParametro = estadoProcesandoParam.IdParametro,
                    Observaciones = "Lote generado automáticamente por Event Grid Cloud Trigger"
                };
                _context.Tickets.Add(ticketAuto);
                await _context.SaveChangesAsync();
            }

            var tipoDocInicial = await _context.Parametros
                .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ParametroConstantes.TipoDocumento.Factura)
                ?? await _context.Parametros.FirstAsync();

            var estadoPendiente = await _context.Parametros
                .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == ParametroConstantes.EstadoDocumento.Pendiente)
                ?? await _context.Parametros.FirstAsync();

            doc = new DocumentoContable
            {
                IdTicket = ticketAuto.IdTicket,
                IdParametroTipoDocumento = tipoDocInicial.IdParametro,
                IdEstadoParametro = estadoPendiente.IdParametro,
                RutaBlobStorage = rutaBlobOUrl,
                NombreArchivo = nombreExtraido,
                HashIntegridad = "cloud-event-grid"
            };

            _context.Documentos.Add(doc);
            ticketAuto.TotalDocumentosProcesados += 1;
            await _context.SaveChangesAsync();
        }

        if (string.IsNullOrWhiteSpace(doc.RutaBlobStorage))
            return ApiResponse<DocumentoDto>.Fail("El documento no tiene una ruta de Blob Storage válida.");

        var stream = await _blobService.DescargarArchivoAsync(doc.RutaBlobStorage);
        if (stream == null)
            return ApiResponse<DocumentoDto>.Fail($"No se pudo descargar el blob desde: {doc.RutaBlobStorage}");

        using (stream)
        {
            // Ejecutar OCR (Azure Document Intelligence)
            var nombreArchivoDoc = doc.NombreArchivo ?? "comprobante.pdf";
            var ocrResult = await _ocrService.ExtraerDatosDocumentoAsync(stream, nombreArchivoDoc);

            var tipoDocParam = await _context.Parametros
                .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ocrResult.TipoDocumentoSugerido)
                ?? await _context.Parametros.FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.TipoDocumento && p.Clave == ParametroConstantes.TipoDocumento.Factura);

            var estadoDocClave = ocrResult.ConfianzaGeneral >= 85.0m
                ? ParametroConstantes.EstadoDocumento.Correcto
                : ParametroConstantes.EstadoDocumento.Observado;

            var estadoDocParam = await _context.Parametros
                .FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == estadoDocClave);

            doc.IdParametroTipoDocumento = tipoDocParam.IdParametro;
            doc.RucEmisor = ocrResult.RucEmisor;
            doc.RazonSocial = ocrResult.RazonSocial;
            doc.SerieComprobante = ocrResult.SerieComprobante;
            doc.NumeroComprobante = ocrResult.NumeroComprobante;
            doc.FechaEmision = ocrResult.FechaEmision;
            doc.MontoSubTotal = ocrResult.MontoSubTotal;
            doc.MontoIgv = ocrResult.MontoIgv;
            doc.MontoTotal = ocrResult.MontoTotal;
            doc.Moneda = ocrResult.Moneda;
            doc.IdEstadoParametro = estadoDocParam.IdParametro;

            doc.CamposExtraidos.Clear();
            foreach (var c in ocrResult.Campos)
            {
                doc.CamposExtraidos.Add(new CampoExtraidoOCR
                {
                    NombreCampo = c.NombreCampo,
                    ValorExtraido = c.Valor,
                    NivelConfianza = c.NivelConfianza,
                    EsCorregido = false
                });
            }

            // Actualizar ticket
            if (doc.Ticket != null)
            {
                var estadoProcesado = await _context.Parametros
                    .FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Procesado);
                doc.Ticket.IdEstadoParametro = estadoProcesado.IdParametro;
            }

            await _context.SaveChangesAsync();

            return ApiResponse<DocumentoDto>.Ok(new DocumentoDto
            {
                IdDocumento = doc.IdDocumento,
                IdTicket = doc.IdTicket,
                CodigoTicket = doc.Ticket?.CodigoTicket ?? "",
                TipoDocumento = tipoDocParam.Clave,
                RucEmisor = doc.RucEmisor,
                RazonSocial = doc.RazonSocial,
                SerieComprobante = doc.SerieComprobante,
                NumeroComprobante = doc.NumeroComprobante,
                FechaEmision = doc.FechaEmision,
                MontoTotal = doc.MontoTotal,
                Moneda = doc.Moneda,
                Estado = estadoDocParam.Clave,
                RutaBlobStorage = doc.RutaBlobStorage,
                NombreArchivo = doc.NombreArchivo,
                FechaCreacion = doc.FechaCreacion
            }, "Documento procesado por OCR reactivo con éxito.");
        }
    }
}
