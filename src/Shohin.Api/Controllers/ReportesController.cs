using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shohin.Application.DTOs.Reportes;
using Shohin.Application.Services;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly ReporteAuditoriaService _reporteService;

    public ReporteAuditoriaService ReporteService => _reporteService;

    public ReportesController(ReporteAuditoriaService reporteService)
    {
        _reporteService = reporteService;
    }

    [HttpGet("auditoria")]
    public async Task<IActionResult> GenerarReporte([FromQuery] ReporteFiltroDto filtro)
    {
        var result = await _reporteService.GenerarReporteAuditoriaAsync(filtro);
        return Ok(result);
    }

    [HttpGet("auditoria/exportar")]
    public async Task<IActionResult> ExportarCsv([FromQuery] ReporteFiltroDto filtro)
    {
        var (contenido, nombreArchivo, contentType) = await _reporteService.ExportarCsvAuditoriaAsync(filtro);
        return File(contenido, contentType, nombreArchivo);
    }
}
