using System.Collections.Generic;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class TicketDigitalizacion : AuditableEntity
{
    public int IdTicket { get; set; }
    public string CodigoTicket { get; set; } = string.Empty;
    public int IdEstadoParametro { get; set; }
    public int TotalDocumentosEsperados { get; set; }
    public int TotalDocumentosProcesados { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public virtual Parametro EstadoParametro { get; set; } = null!;
    public virtual ICollection<DocumentoContable> Documentos { get; set; } = new List<DocumentoContable>();
    public virtual ICollection<RevisionTicket> Revisiones { get; set; } = new List<RevisionTicket>();
}
