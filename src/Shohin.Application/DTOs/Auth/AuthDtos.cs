namespace Shohin.Application.DTOs.Auth;

public class LoginRequestDto
{
    public string CodigoUsuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public string CodigoUsuario { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string TipoUsuario { get; set; } = string.Empty;
}

public class UsuarioDto
{
    public int IdUsuario { get; set; }
    public string CodigoUsuario { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string TipoUsuario { get; set; } = string.Empty;
    public bool Estado { get; set; }
}

public class CrearUsuarioDto
{
    public string CodigoUsuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public int IdRol { get; set; }
    public int IdParametroTipoUsuario { get; set; }
}
