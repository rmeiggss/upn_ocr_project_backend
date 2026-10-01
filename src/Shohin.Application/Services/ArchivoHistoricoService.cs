using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Documentos;
using Shohin.Application.DTOs.Historico;
using Shohin.Application.DTOs.Tickets;
using Shohin.Application.Interfaces;
using Shohin.Domain.Entities;
using Shohin.Domain.Enums;

namespace Shohin.Application.Services;

public class ArchivoHistoricoService
{
    private readonly IApplicationDbContext _context;

    public ArchivoHistoricoService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<DocumentoDto>>> ConsultarHistoricoAsync(ConsultaHistoricoFiltroDto filtro)
    {
        var query = _context.Documentos
            .Include(d => d.Ticket)
            .Include(d => d.TipoDocumentoParametro)
            .Include(d => d.EstadoParametro)
            .Include(d => d.CamposExtraidos)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.RucEmisor))
            query = query.Where(d => d.RucEmisor != null && d.RucEmisor.Contains(filtro.RucEmisor));

        if (!string.IsNullOrWhiteSpace(filtro.RazonSocial))
            query = query.Where(d => d.RazonSocial != null && d.RazonSocial.Contains(filtro.RazonSocial));

        if (!string.IsNullOrWhiteSpace(filtro.TipoDocumento))
            query = query.Where(d => d.TipoDocumentoParametro.Clave == filtro.TipoDocumento);

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
            query = query.Where(d => d.EstadoParametro.Clave == filtro.Estado);

        if (!string.IsNullOrWhiteSpace(filtro.CodigoTicket))
            query = query.Where(d => d.Ticket != null && d.Ticket.CodigoTicket == filtro.CodigoTicket);

        if (filtro.FechaDesde.HasValue)
            query = query.Where(d => d.FechaEmision >= filtro.FechaDesde.Value);

        if (filtro.FechaHasta.HasValue)
            query = query.Where(d => d.FechaEmision <= filtro.FechaHasta.Value);

        var skip = (filtro.Pagina - 1) * filtro.RegistrosPorPagina;
        var resultados = await query
            .OrderByDescending(d => d.FechaEmision)
            .Skip(skip)
            .Take(filtro.RegistrosPorPagina)
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

        return ApiResponse<List<DocumentoDto>>.Ok(resultados);
    }

    // Si el documento no se encuentra en el archivo histórico digital, se crea un ticket de búsqueda física en almacén
    public async Task<ApiResponse<TicketDto>> CrearSolicitudBusquedaFisicaAsync(SolicitudBusquedaFisicaDto solicitud)
    {
        var estadoPendiente = await _context.Parametros
            .FirstAsync(p => p.Grupo == ParametroConstantes.Grupos.EstadoTicket && p.Clave == ParametroConstantes.EstadoTicket.Pendiente);

        var codigoTicket = $"SOL-BUSQ-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}";
        var obs = $"Solicitud de búsqueda física para RUC: {solicitud.RucEmisor}, Serie/N°: {solicitud.SerieNumero ?? "N/A"}, Período: {solicitud.AnioPeriodo ?? "N/A"}. Motivo: {solicitud.MotivoSolicitud}";

        var ticket = new TicketDigitalizacion
        {
            CodigoTicket = codigoTicket,
            IdEstadoParametro = estadoPendiente.IdParametro,
            TotalDocumentosEsperados = 1,
            TotalDocumentosProcesados = 0,
            Observaciones = obs
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        return ApiResponse<TicketDto>.Ok(new TicketDto
        {
            IdTicket = ticket.IdTicket,
            CodigoTicket = ticket.CodigoTicket,
            Estado = estadoPendiente.Clave,
            TotalDocumentosEsperados = 1,
            TotalDocumentosProcesados = 0,
            Observaciones = ticket.Observaciones,
            FechaCreacion = ticket.FechaCreacion,
            UsuarioCreacion = ticket.UsuarioCreacion
        }, "Solicitud de búsqueda física registrada y asignada al personal de archivo.");
    }
}
