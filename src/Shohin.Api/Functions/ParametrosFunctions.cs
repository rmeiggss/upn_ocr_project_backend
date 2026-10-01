using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

public class ParametrosFunctions
{
    private readonly ParametroService _parametroService;

    public ParametrosFunctions(ParametroService parametroService)
    {
        _parametroService = parametroService;
    }

    [Function("Parametros_ObtenerTodos")]
    public async Task<IActionResult> ObtenerTodos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parametros")] HttpRequest req)
    {
        var result = await _parametroService.ObtenerTodosAsync();
        return new OkObjectResult(result);
    }

    [Function("Parametros_ObtenerPorGrupo")]
    public async Task<IActionResult> ObtenerPorGrupo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parametros/grupo/{grupo}")] HttpRequest req,
        string grupo)
    {
        var result = await _parametroService.ObtenerPorGrupoAsync(grupo);
        return new OkObjectResult(result);
    }
}
