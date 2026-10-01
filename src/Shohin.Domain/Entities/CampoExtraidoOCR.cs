using Shohin.Domain.Common;

namespace Shohin.Domain.Entities;

public class CampoExtraidoOCR : AuditableEntity
{
    public int IdCampoExtraido { get; set; }
    public int IdDocumento { get; set; }
    public string NombreCampo { get; set; } = string.Empty;
    public string? ValorExtraido { get; set; }
    public string? ValorCorregido { get; set; } // CUS-05
    public decimal NivelConfianza { get; set; } // 0.00 a 100.00 %
    public bool EsCorregido { get; set; }

    // Navegación (Composición: Cascade Delete)
    public virtual DocumentoContable Documento { get; set; } = null!;
}
