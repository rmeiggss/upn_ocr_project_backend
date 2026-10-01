using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Shohin.Application.DTOs.Historico;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

public class HistoricoFunctions
{
    private readonly ArchivoHistoricoService _historicoService;

    public HistoricoFunctions(ArchivoHistoricoService historicoService)
    {
        _historicoService = historicoService;
    }

    [Function("Historico_Buscar")]
    public async Task<IActionResult> BuscarDocumentos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "historico/buscar")] HttpRequest req)
    {
        var filtro = new ConsultaHistoricoFiltroDto
        {
            RucEmisor = req.Query["rucEmisor"].ToString(),
            RazonSocial = req.Query["razonSocial"].ToString(),
            TipoDocumento = req.Query["tipoDocumento"].ToString(),
            Estado = req.Query["estado"].ToString(),
            CodigoTicket = req.Query["codigoTicket"].ToString()
        };

        if (DateTime.TryParse(req.Query["fechaDesde"], out var desde))
            filtro.FechaDesde = desde;

        if (DateTime.TryParse(req.Query["fechaHasta"], out var hasta))
            filtro.FechaHasta = hasta;

        if (int.TryParse(req.Query["pagina"], out var pag))
            filtro.Pagina = pag;

        if (int.TryParse(req.Query["registrosPorPagina"], out var tam))
            filtro.RegistrosPorPagina = tam;

        var result = await _historicoService.ConsultarHistoricoAsync(filtro);
        return new OkObjectResult(result);
    }

    [Function("Historico_SolicitudBusqueda")]
    public async Task<IActionResult> SolicitarBusquedaFisica(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "historico/solicitud-busqueda")] HttpRequest req)
    {
        var solicitud = await req.ReadFromJsonAsync<SolicitudBusquedaFisicaDto>();
        if (solicitud == null)
            return new BadRequestObjectResult("Cuerpo de solicitud inválido.");

        var result = await _historicoService.CrearSolicitudBusquedaFisicaAsync(solicitud);
        return new OkObjectResult(result);
    }
}
