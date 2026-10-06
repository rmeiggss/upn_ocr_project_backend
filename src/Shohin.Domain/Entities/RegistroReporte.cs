using System;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class RegistroReporte : AuditableEntity
{
    public int IdReporte { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;
    public string FormatoArchivo { get; set; } = string.Empty; // 'PDF', 'EXCEL', 'CSV'
    public string? FiltroUsuario { get; set; }

    // Navegación
    public virtual Usuario Usuario { get; set; } = null!;
}
