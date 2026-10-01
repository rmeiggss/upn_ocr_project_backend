using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shohin.Application.DTOs.Auth;
using Shohin.Application.DTOs.Common;
using Shohin.Application.Interfaces;
using Shohin.Domain.Entities;
using Shohin.Domain.Enums;

namespace Shohin.Application.Services;

public class AuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    public AuthService(IApplicationDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.TipoUsuarioParametro)
            .FirstOrDefaultAsync(u => u.CodigoUsuario == request.CodigoUsuario && u.Estado);

        if (usuario == null || !_jwtService.ValidarPassword(request.Password, usuario.PasswordHash))
        {
            return ApiResponse<LoginResponseDto>.Fail("Credenciales inválidas o usuario inactivo.");
        }

        var token = _jwtService.GenerarToken(usuario);

        var response = new LoginResponseDto
        {
            Token = token,
            IdUsuario = usuario.IdUsuario,
            CodigoUsuario = usuario.CodigoUsuario,
            Nombres = usuario.Nombres,
            Correo = usuario.Correo,
            Rol = usuario.Rol.NombreRol,
            TipoUsuario = usuario.TipoUsuarioParametro.Clave
        };

        return ApiResponse<LoginResponseDto>.Ok(response, "Inicio de sesión exitoso.");
    }

    public async Task<ApiResponse<List<UsuarioDto>>> ObtenerUsuariosAsync()
    {
        var usuarios = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.TipoUsuarioParametro)
            .Select(u => new UsuarioDto
            {
                IdUsuario = u.IdUsuario,
                CodigoUsuario = u.CodigoUsuario,
                Nombres = u.Nombres,
                Correo = u.Correo,
                Rol = u.Rol.NombreRol,
                TipoUsuario = u.TipoUsuarioParametro.Clave,
                Estado = u.Estado
            })
            .ToListAsync();

        return ApiResponse<List<UsuarioDto>>.Ok(usuarios);
    }

    public async Task<ApiResponse<UsuarioDto>> CrearUsuarioAsync(CrearUsuarioDto request)
    {
        var existe = await _context.Usuarios.AnyAsync(u => u.CodigoUsuario == request.CodigoUsuario);
        if (existe)
        {
            return ApiResponse<UsuarioDto>.Fail("El código de usuario ya se encuentra registrado.");
        }

        var usuario = new Usuario
        {
            CodigoUsuario = request.CodigoUsuario,
            PasswordHash = _jwtService.HashPassword(request.Password),
            Nombres = request.Nombres,
            Correo = request.Correo,
            IdRol = request.IdRol,
            IdParametroTipoUsuario = request.IdParametroTipoUsuario,
            Estado = true
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var usuarioCreado = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.TipoUsuarioParametro)
            .FirstAsync(u => u.IdUsuario == usuario.IdUsuario);

        return ApiResponse<UsuarioDto>.Ok(new UsuarioDto
        {
            IdUsuario = usuarioCreado.IdUsuario,
            CodigoUsuario = usuarioCreado.CodigoUsuario,
            Nombres = usuarioCreado.Nombres,
            Correo = usuarioCreado.Correo,
            Rol = usuarioCreado.Rol.NombreRol,
            TipoUsuario = usuarioCreado.TipoUsuarioParametro.Clave,
            Estado = usuarioCreado.Estado
        }, "Usuario creado correctamente.");
    }
}
