using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Documentos;
using Shohin.Application.Interfaces;
using Shohin.Domain.Entities;
using Shohin.Domain.Enums;

namespace Shohin.Application.Services;

public class ValidacionService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ValidacionService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ApiResponse<List<DocumentoDto>>> ObtenerDocumentosPorTicketAsync(int idTicket)
    {
        var documentos = await _context.Documentos
            .Include(d => d.Ticket)
            .Include(d => d.TipoDocumentoParametro)
            .Include(d => d.EstadoParametro)
            .Include(d => d.CamposExtraidos)
            .Where(d => d.IdTicket == idTicket)
            .OrderBy(d => d.IdDocumento)
            .Select(d => new DocumentoDto
            {
                IdDocumento = d.IdDocumento,
                IdTicket = d.IdTicket,
                CodigoTicket = d.Ticket != null ? d.Ticket.CodigoTicket : string.Empty,
                TipoDocumento = d.TipoDocumentoParametro.Clave,
                RucEmisor = d.RucEmisor,
                RazonSocial = d.RazonSocial,
                SerieComprobante = d.SerieComprobante,
                NumeroComprobante = d.NumeroComprobante,
                FechaEmision = d.FechaEmision,
                MontoTotal = d.MontoTotal,
                Moneda = d.Moneda,
                Estado = d.EstadoParametro.Clave,
                RutaBlobStorage = d.RutaBlobStorage,
                NombreArchivo = d.NombreArchivo,
                FechaCreacion = d.FechaCreacion,
                Campos = d.CamposExtraidos.Select(c => new CampoOcrDto
                {
                    IdCampoExtraido = c.IdCampoExtraido,
                    NombreCampo = c.NombreCampo,
                    ValorExtraido = c.ValorExtraido,
                    ValorCorregido = c.ValorCorregido,
                    NivelConfianza = c.NivelConfianza,
                    EsCorregido = c.EsCorregido
                }).ToList()
            })
            .ToListAsync();

        return ApiResponse<List<DocumentoDto>>.Ok(documentos);
    }

    public async Task<ApiResponse<DocumentoDto>> ObtenerDocumentoPorIdAsync(int idDocumento)
    {
        var d = await _context.Documentos
            .Include(doc => doc.Ticket)
            .Include(doc => doc.TipoDocumentoParametro)
            .Include(doc => doc.EstadoParametro)
            .Include(doc => doc.CamposExtraidos)
            .FirstOrDefaultAsync(doc => doc.IdDocumento == idDocumento);

        if (d == null)
            return ApiResponse<DocumentoDto>.Fail("Documento no encontrado.");

        return ApiResponse<DocumentoDto>.Ok(new DocumentoDto
        {
            IdDocumento = d.IdDocumento,
            IdTicket = d.IdTicket,
            CodigoTicket = d.Ticket?.CodigoTicket ?? string.Empty,
            TipoDocumento = d.TipoDocumentoParametro.Clave,
            RucEmisor = d.RucEmisor,
            RazonSocial = d.RazonSocial,
            SerieComprobante = d.SerieComprobante,
            NumeroComprobante = d.NumeroComprobante,
            FechaEmision = d.FechaEmision,
            MontoTotal = d.MontoTotal,
            Moneda = d.Moneda,
            Estado = d.EstadoParametro.Clave,
            RutaBlobStorage = d.RutaBlobStorage,
            NombreArchivo = d.NombreArchivo,
            FechaCreacion = d.FechaCreacion,
            Campos = d.CamposExtraidos.Select(c => new CampoOcrDto
            {
                IdCampoExtraido = c.IdCampoExtraido,
                NombreCampo = c.NombreCampo,
                ValorExtraido = c.ValorExtraido,
                ValorCorregido = c.ValorCorregido,
                NivelConfianza = c.NivelConfianza,
                EsCorregido = c.EsCorregido
            }).ToList()
        });
    }

    // CUS-05: Corregir datos extraídos
    public async Task<ApiResponse<CampoOcrDto>> CorregirCampoOcrAsync(CorregirCampoDto request)
    {
        var campo = await _context.CamposOCR
            .Include(c => c.Documento)
            .FirstOrDefaultAsync(c => c.IdCampoExtraido == request.IdCampoExtraido);

        if (campo == null)
            return ApiResponse<CampoOcrDto>.Fail("Campo OCR no encontrado.");

        campo.ValorCorregido = request.ValorCorregido;
        campo.EsCorregido = true;

        // Actualizar el valor directo en el documento si corresponde
        var doc = campo.Documento;
        switch (campo.NombreCampo)
        {
            case "RucEmisor":
                doc.RucEmisor = request.ValorCorregido;
                break;
            case "RazonSocial":
                doc.RazonSocial = request.ValorCorregido;
                break;
            case "SerieComprobante":
                doc.SerieComprobante = request.ValorCorregido;
                break;
            case "NumeroComprobante":
                doc.NumeroComprobante = request.ValorCorregido;
                break;
            case "MontoTotal":
                if (decimal.TryParse(request.ValorCorregido, out var monto))
                    doc.MontoTotal = monto;
                break;
            case "MontoIgv":
                if (decimal.TryParse(request.ValorCorregido, out var igv))
                    doc.MontoIgv = igv;
                break;
            case "MontoSubTotal":
                if (decimal.TryParse(request.ValorCorregido, out var sub))
                    doc.MontoSubTotal = sub;
                break;
        }

        await _context.SaveChangesAsync();

        return ApiResponse<CampoOcrDto>.Ok(new CampoOcrDto
        {
            IdCampoExtraido = campo.IdCampoExtraido,
            NombreCampo = campo.NombreCampo,
            ValorExtraido = campo.ValorExtraido,
            ValorCorregido = campo.ValorCorregido,
            NivelConfianza = 100.00m,
            EsCorregido = true
        }, "Campo corregido exitosamente.");
    }

    // CUS-02: Validar documento procesado
    public async Task<ApiResponse<bool>> ValidarDocumentoAsync(int idDocumento, ValidarDocumentoDto request)
    {
        var doc = await _context.Documentos
            .Include(d => d.Ticket)
            .FirstOrDefaultAsync(d => d.IdDocumento == idDocumento);

        if (doc == null)
            return ApiResponse<bool>.Fail("Documento no encontrado.");

        var estado = await _context.Parametros
            .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == request.Decision);

        if (estado == null)
            return ApiResponse<bool>.Fail($"Estado inválido: {request.Decision}");

        doc.IdEstadoParametro = estado.IdParametro;

        // Si el documento pertenece a un ticket, registramos la revisión
        if (doc.IdTicket.HasValue)
        {
            var username = _currentUserService.GetCurrentUsername();
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.CodigoUsuario == username);
            if (usuario != null)
            {
                var revision = new RevisionTicket
                {
                    IdTicket = doc.IdTicket.Value,
                    IdUsuario = usuario.IdUsuario,
                    FechaInicioRevision = DateTime.UtcNow,
                    FechaFinRevision = DateTime.UtcNow,
                    ResultadoAprobacion = request.Decision,
                    Observaciones = request.MotivoObservacion
                };
                _context.Revisiones.Add(revision);
            }
        }

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, $"Documento actualizado a estado '{request.Decision}'.");
    }
}
