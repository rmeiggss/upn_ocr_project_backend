using Shohin.Domain.Entities;

namespace Shohin.Application.Interfaces;

public interface IJwtService
{
    string GenerarToken(Usuario usuario);
    bool ValidarPassword(string password, string passwordHash);
    string HashPassword(string password);
}

public interface ICurrentUserService
{
    string GetCurrentUsername();
    string? GetCurrentRole();
}
