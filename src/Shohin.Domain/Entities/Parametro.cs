using System.Collections.Generic;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class Parametro : AuditableEntity
{
    public int IdParametro { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;

    // Relaciones
    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public virtual ICollection<TicketDigitalizacion> Tickets { get; set; } = new List<TicketDigitalizacion>();
    public virtual ICollection<DocumentoContable> DocumentosPorTipo { get; set; } = new List<DocumentoContable>();
    public virtual ICollection<DocumentoContable> DocumentosPorEstado { get; set; } = new List<DocumentoContable>();
}
