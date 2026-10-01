using System.Collections.Generic;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class Rol : AuditableEntity
{
    public int IdRol { get; set; }
    public string NombreRol { get; set; } = string.Empty;
    public bool Estado { get; set; } = true;

    // Relaciones
    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
