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
                MontoSubTotal = d.MontoSubTotal,
                MontoIgv = d.MontoIgv,
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

    public async Task<ApiResponse<DocumentoPaginadoDto>> ObtenerDocumentosPorTicketPaginadoAsync(
        int idTicket,
        int pagina = 1,
        int tamanoPagina = 10,
        string? filtro = null)
    {
        var query = _context.Documentos
            .Include(d => d.Ticket)
            .Include(d => d.TipoDocumentoParametro)
            .Include(d => d.EstadoParametro)
            .Include(d => d.CamposExtraidos)
            .Where(d => d.IdTicket == idTicket);

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            query = query.Where(d =>
                (d.SerieComprobante != null && d.SerieComprobante.ToLower().Contains(f)) ||
                (d.NumeroComprobante != null && d.NumeroComprobante.ToLower().Contains(f)) ||
                (d.RucEmisor != null && d.RucEmisor.ToLower().Contains(f)) ||
                (d.RazonSocial != null && d.RazonSocial.ToLower().Contains(f)) ||
                (d.NombreArchivo != null && d.NombreArchivo.ToLower().Contains(f)));
        }

        var totalRegistros = await query.CountAsync();

        if (pagina < 1) pagina = 1;
        if (tamanoPagina < 1) tamanoPagina = 10;

        var items = await query
            .OrderBy(d => d.IdDocumento)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
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
                MontoSubTotal = d.MontoSubTotal,
                MontoIgv = d.MontoIgv,
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

        var paginado = new DocumentoPaginadoDto
        {
            Items = items,
            TotalRegistros = totalRegistros,
            Pagina = pagina,
            TamanoPagina = tamanoPagina,
            TotalPaginas = tamanoPagina > 0 ? (int)Math.Ceiling((double)totalRegistros / tamanoPagina) : 1
        };

        return ApiResponse<DocumentoPaginadoDto>.Ok(paginado);
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
            MontoSubTotal = d.MontoSubTotal,
            MontoIgv = d.MontoIgv,
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

        var decision = request.ObtenerDecision();

        var estado = await _context.Parametros
            .FirstOrDefaultAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoDocumento && p.Clave == decision);

        if (estado == null)
            return ApiResponse<bool>.Fail($"Estado inválido: {decision}");

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
                    ResultadoAprobacion = decision,
                    Observaciones = request.MotivoObservacion ?? request.Comentarios
                };
                _context.Revisiones.Add(revision);
            }
        }

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, $"Documento actualizado a estado '{decision}'.");
    }
}
