using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shohin.Application.DTOs.Auth;
using Shohin.Application.Services;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Exito)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpGet("usuarios")]
    [Authorize]
    public async Task<IActionResult> ObtenerUsuarios()
    {
        var result = await _authService.ObtenerUsuariosAsync();
        return Ok(result);
    }

    [HttpPost("usuarios")]
    [Authorize]
    public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto request)
    {
        var result = await _authService.CrearUsuarioAsync(request);
        if (!result.Exito)
            return BadRequest(result);

        return Ok(result);
    }
}
