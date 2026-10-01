using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shohin.Application.Interfaces;
using Shohin.Domain.Entities;

namespace Shohin.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerarToken(Usuario usuario)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "ShohinEnterpriseSuperSecretKey2026_ArquitecturaUPN";
        var issuer = _configuration["Jwt:Issuer"] ?? "ShohinApi";
        var audience = _configuration["Jwt:Audience"] ?? "ShohinApp";
        var expiryHours = int.TryParse(_configuration["Jwt:ExpiryHours"], out var hours) ? hours : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.CodigoUsuario),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Correo),
            new Claim(ClaimTypes.Name, usuario.Nombres),
            new Claim(ClaimTypes.Role, usuario.Rol?.NombreRol ?? "Usuario"),
            new Claim("IdUsuario", usuario.IdUsuario.ToString()),
            new Claim("TipoUsuario", usuario.TipoUsuarioParametro?.Clave ?? "INTERNO"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool ValidarPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
