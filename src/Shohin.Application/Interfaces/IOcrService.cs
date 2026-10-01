using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Shohin.Application.Interfaces;

public class OcrCampoExtraidoResult
{
    public string NombreCampo { get; set; } = string.Empty;
    public string? Valor { get; set; }
    public decimal NivelConfianza { get; set; }
}

public class OcrDocumentoResult
{
    public string? RucEmisor { get; set; }
    public string? RazonSocial { get; set; }
    public string? SerieComprobante { get; set; }
    public string? NumeroComprobante { get; set; }
    public DateTime? FechaEmision { get; set; }
    public decimal? MontoSubTotal { get; set; }
    public decimal? MontoIgv { get; set; }
    public decimal? MontoTotal { get; set; }
    public string Moneda { get; set; } = "PEN";
    public string TipoDocumentoSugerido { get; set; } = "FACTURA";
    public decimal ConfianzaGeneral { get; set; }
    public List<OcrCampoExtraidoResult> Campos { get; set; } = new();
}

public interface IOcrService
{
    Task<OcrDocumentoResult> ExtraerDatosDocumentoAsync(Stream archivoStream, string nombreArchivo);
}
