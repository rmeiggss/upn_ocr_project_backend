using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Shohin.Application.DTOs.Reportes;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

public class ReportesFunctions
{
    private readonly ReporteAuditoriaService _reporteService;

    public ReportesFunctions(ReporteAuditoriaService reporteService)
    {
        _reporteService = reporteService;
    }

    private ReporteFiltroDto ExtraerFiltro(HttpRequest req)
    {
        var filtro = new ReporteFiltroDto
        {
            TipoDocumento = req.Query["tipoDocumento"].ToString(),
            RucEmisor = req.Query["rucEmisor"].ToString()
        };

        if (DateTime.TryParse(req.Query["fechaInicio"], out var inicio))
            filtro.FechaInicio = inicio;

        if (DateTime.TryParse(req.Query["fechaFin"], out var fin))
            filtro.FechaFin = fin;

        return filtro;
    }

    [Function("Reportes_Auditoria")]
    public async Task<IActionResult> GenerarReporte(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reportes/auditoria")] HttpRequest req)
    {
        var filtro = ExtraerFiltro(req);
        var result = await _reporteService.GenerarReporteAuditoriaAsync(filtro);
        return new OkObjectResult(result);
    }

    [Function("Reportes_ExportarCsv")]
    public async Task<IActionResult> ExportarCsv(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reportes/auditoria/exportar")] HttpRequest req)
    {
        var filtro = ExtraerFiltro(req);
        var (contenido, nombreArchivo, contentType) = await _reporteService.ExportarCsvAuditoriaAsync(filtro);
        return new FileContentResult(contenido, contentType)
        {
            FileDownloadName = nombreArchivo
        };
    }
}
