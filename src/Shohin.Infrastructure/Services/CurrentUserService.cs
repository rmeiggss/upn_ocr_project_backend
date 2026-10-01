using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentUsername()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var name = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user?.FindFirst(ClaimTypes.Name)?.Value
                   ?? user?.Identity?.Name;

        return name ?? "SYSTEM";
    }

    public string? GetCurrentRole()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
    }
}
