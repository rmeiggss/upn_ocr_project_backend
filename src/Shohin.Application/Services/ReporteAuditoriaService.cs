using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Reportes;
using Shohin.Application.Interfaces;
using Shohin.Domain.Enums;

namespace Shohin.Application.Services;

public class ReporteAuditoriaService
{
    private readonly IApplicationDbContext _context;

    public ReporteAuditoriaService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ResumenReporteAuditoriaDto>> GenerarReporteAuditoriaAsync(ReporteFiltroDto filtro)
    {
        var query = _context.Documentos
            .Include(d => d.Ticket)
            .Include(d => d.TipoDocumentoParametro)
            .Include(d => d.EstadoParametro)
            .Include(d => d.CamposExtraidos)
            .AsQueryable();

        if (filtro.FechaInicio.HasValue)
            query = query.Where(d => d.FechaEmision >= filtro.FechaInicio.Value);

        if (filtro.FechaFin.HasValue)
            query = query.Where(d => d.FechaEmision <= filtro.FechaFin.Value);

        if (!string.IsNullOrWhiteSpace(filtro.TipoDocumento))
            query = query.Where(d => d.TipoDocumentoParametro.Clave == filtro.TipoDocumento);

        if (!string.IsNullOrWhiteSpace(filtro.RucEmisor))
            query = query.Where(d => d.RucEmisor != null && d.RucEmisor.Contains(filtro.RucEmisor));

        var listaDocs = await query.ToListAsync();

        var items = listaDocs.Select(d =>
        {
            var promedioConfianza = d.CamposExtraidos.Any()
                ? d.CamposExtraidos.Average(c => c.NivelConfianza)
                : 0.00m;

            return new ItemReporteAuditoriaDto
            {
                CodigoTicket = d.Ticket?.CodigoTicket ?? "S/T",
                TipoDocumento = d.TipoDocumentoParametro.Clave,
                RucEmisor = d.RucEmisor ?? "N/A",
                RazonSocial = d.RazonSocial ?? "N/A",
                SerieNumero = $"{d.SerieComprobante}-{d.NumeroComprobante}",
                FechaEmision = d.FechaEmision,
                MontoTotal = d.MontoTotal ?? 0.00m,
                Estado = d.EstadoParametro.Clave,
                HashIntegridad = d.HashIntegridad ?? "N/A",
                PromedioConfianzaOcr = Math.Round(promedioConfianza, 2)
            };
        }).ToList();

        var total = items.Count;
        var totalCorrectos = items.Count(i => i.Estado == ParametroConstantes.EstadoDocumento.Correcto);
        var totalObservados = items.Count(i => i.Estado == ParametroConstantes.EstadoDocumento.Observado);
        var montoAcumulado = items.Sum(i => i.MontoTotal);
        var porcentaje = total > 0 ? Math.Round(((decimal)totalCorrectos / total) * 100, 2) : 100.00m;

        var resumen = new ResumenReporteAuditoriaDto
        {
            TotalDocumentos = total,
            MontoAcumulado = montoAcumulado,
            TotalCorrectos = totalCorrectos,
            TotalObservados = totalObservados,
            PorcentajeConfiabilidad = porcentaje,
            Items = items
        };

        return ApiResponse<ResumenReporteAuditoriaDto>.Ok(resumen);
    }

    public async Task<(byte[] Contenido, string NombreArchivo, string ContentType)> ExportarCsvAuditoriaAsync(ReporteFiltroDto filtro)
    {
        var dataResult = await GenerarReporteAuditoriaAsync(filtro);
        var resumen = dataResult.Datos!;

        var sb = new StringBuilder();
        sb.AppendLine("CodigoTicket,TipoDocumento,RucEmisor,RazonSocial,SerieNumero,FechaEmision,MontoTotal,Estado,PromedioConfianzaOCR,HashIntegridad");

        foreach (var item in resumen.Items)
        {
            sb.AppendLine($"\"{item.CodigoTicket}\",\"{item.TipoDocumento}\",\"{item.RucEmisor}\",\"{item.RazonSocial}\",\"{item.SerieNumero}\",\"{item.FechaEmision:yyyy-MM-dd}\",{item.MontoTotal},\"{item.Estado}\",{item.PromedioConfianzaOcr},\"{item.HashIntegridad}\"");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var nombre = $"Reporte_Auditoria_SUNAT_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return (bytes, nombre, "text/csv");
    }
}
