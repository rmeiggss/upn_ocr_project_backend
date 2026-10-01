using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shohin.Application.Services;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParametrosController : ControllerBase
{
    private readonly ParametroService _parametroService;

    public ParametrosController(ParametroService parametroService)
    {
        _parametroService = parametroService;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos()
    {
        var result = await _parametroService.ObtenerTodosAsync();
        return Ok(result);
    }

    [HttpGet("grupo/{grupo}")]
    public async Task<IActionResult> ObtenerPorGrupo(string grupo)
    {
        var result = await _parametroService.ObtenerPorGrupoAsync(grupo);
        return Ok(result);
    }
}
