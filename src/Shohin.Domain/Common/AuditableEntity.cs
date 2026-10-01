using System;

namespace Shohin.Domain.Common;

/// <summary>
/// Clase base para auditoría transversal automática.
/// Interceptada por SaveChangesAsync en ApplicationDbContext.
/// </summary>
public abstract class AuditableEntity
{
    public string UsuarioCreacion { get; set; } = "SYSTEM";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
