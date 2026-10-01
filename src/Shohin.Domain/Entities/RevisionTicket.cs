using System;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class RevisionTicket : AuditableEntity
{
    public int IdRevision { get; set; }
    public int IdTicket { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FechaInicioRevision { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFinRevision { get; set; }
    public string? ResultadoAprobacion { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public virtual TicketDigitalizacion Ticket { get; set; } = null!;
    public virtual Usuario Usuario { get; set; } = null!;
}
