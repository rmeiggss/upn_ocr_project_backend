using System;
using System.Collections.Generic;
using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class DocumentoContable : AuditableEntity
{
    public int IdDocumento { get; set; }
    public int? IdTicket { get; set; } // Agregación (puede ser null)
    public int IdParametroTipoDocumento { get; set; } // Discriminador TPH
    public string? RucEmisor { get; set; }
    public string? RazonSocial { get; set; }
    public string? SerieComprobante { get; set; }
    public string? NumeroComprobante { get; set; }
    public DateTime? FechaEmision { get; set; }
    public decimal? MontoSubTotal { get; set; }
    public decimal? MontoIgv { get; set; }
    public decimal? MontoTotal { get; set; }
    public string Moneda { get; set; } = "PEN";
    public int IdEstadoParametro { get; set; }
    public string? RutaBlobStorage { get; set; }
    public string? NombreArchivo { get; set; }
    public string? HashIntegridad { get; set; } // SHA-256

    // Herencia TPH (NotaCredito) y Metadatos OCR
    public string? NumeroFacturaReferencia { get; set; }
    public string? MotivoAnulacion { get; set; }
    public decimal? ScoreConfianza { get; set; }

    // Navegación
    public virtual TicketDigitalizacion? Ticket { get; set; }
    public virtual Parametro TipoDocumentoParametro { get; set; } = null!;
    public virtual Parametro EstadoParametro { get; set; } = null!;
    public virtual ICollection<CampoExtraidoOCR> CamposExtraidos { get; set; } = new List<CampoExtraidoOCR>();
}
