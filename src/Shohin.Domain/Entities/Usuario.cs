using System.Collections.Generic;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class Usuario : AuditableEntity
{
    public int IdUsuario { get; set; }
    public int IdRol { get; set; }
    public int IdParametroTipoUsuario { get; set; } // Discriminador TPH
    public string CodigoUsuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool Estado { get; set; } = true;

    // Navegación
    public virtual Rol Rol { get; set; } = null!;
    public virtual Parametro TipoUsuarioParametro { get; set; } = null!;
    public virtual ICollection<RevisionTicket> Revisiones { get; set; } = new List<RevisionTicket>();
}
