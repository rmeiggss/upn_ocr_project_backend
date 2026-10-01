using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Shohin.Application.DTOs.Auth;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

public class AuthFunctions
{
    private readonly AuthService _authService;

    public AuthFunctions(AuthService authService)
    {
        _authService = authService;
    }

    [Function("Auth_Login")]
    public async Task<IActionResult> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<LoginRequestDto>();
        if (request == null)
            return new BadRequestObjectResult("Cuerpo de solicitud inválido.");

        var result = await _authService.LoginAsync(request);
        if (!result.Exito)
            return new UnauthorizedObjectResult(result);

        return new OkObjectResult(result);
    }

    [Function("Auth_ObtenerUsuarios")]
    public async Task<IActionResult> ObtenerUsuarios(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/usuarios")] HttpRequest req)
    {
        var result = await _authService.ObtenerUsuariosAsync();
        return new OkObjectResult(result);
    }

    [Function("Auth_CrearUsuario")]
    public async Task<IActionResult> CrearUsuario(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/usuarios")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<CrearUsuarioDto>();
        if (request == null)
            return new BadRequestObjectResult("Cuerpo de solicitud inválido.");

        var result = await _authService.CrearUsuarioAsync(request);
        if (!result.Exito)
            return new BadRequestObjectResult(result);

        return new OkObjectResult(result);
    }
}
