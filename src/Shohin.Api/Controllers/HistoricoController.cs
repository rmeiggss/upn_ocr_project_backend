using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shohin.Application.DTOs.Historico;
using Shohin.Application.Services;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoricoController : ControllerBase
{
    private readonly ArchivoHistoricoService _historicoService;

    public HistoricoController(ArchivoHistoricoService historicoService)
    {
        _historicoService = historicoService;
    }

    [HttpGet("buscar")]
    public async Task<IActionResult> BuscarDocumentos([FromQuery] ConsultaHistoricoFiltroDto filtro)
    {
        var result = await _historicoService.ConsultarHistoricoAsync(filtro);
        return Ok(result);
    }

    [HttpPost("solicitud-busqueda")]
    public async Task<IActionResult> SolicitarBusquedaFisica([FromBody] SolicitudBusquedaFisicaDto solicitud)
    {
        var result = await _historicoService.CrearSolicitudBusquedaFisicaAsync(solicitud);
        return Ok(result);
    }
}
