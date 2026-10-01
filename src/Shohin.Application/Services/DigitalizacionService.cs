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
}
