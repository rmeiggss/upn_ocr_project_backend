using System;
using System.Collections.Generic;

namespace Shohin.Application.DTOs.Documentos;

public class CampoOcrDto
{
    public int IdCampoExtraido { get; set; }
    public string NombreCampo { get; set; } = string.Empty;
    public string? ValorExtraido { get; set; }
    public string? ValorCorregido { get; set; }
    public decimal NivelConfianza { get; set; }
    public bool EsCorregido { get; set; }
}

public class DocumentoDto
{
    public int IdDocumento { get; set; }
    public int? IdTicket { get; set; }
    public string CodigoTicket { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string? RucEmisor { get; set; }
    public string? RazonSocial { get; set; }
    public string? SerieComprobante { get; set; }
    public string? NumeroComprobante { get; set; }
    public DateTime? FechaEmision { get; set; }
    public decimal? MontoSubTotal { get; set; }
    public decimal? MontoIgv { get; set; }
    public decimal? MontoTotal { get; set; }

    // Aliases computados para interoperabilidad y consistencia con frontend Angular
    public decimal? Subtotal => MontoSubTotal;
    public decimal? Igv => MontoIgv;
    public decimal? Total => MontoTotal;
    public string SerieNumero => !string.IsNullOrEmpty(SerieComprobante) && !string.IsNullOrEmpty(NumeroComprobante)
        ? $"{SerieComprobante}-{NumeroComprobante}"
        : (!string.IsNullOrEmpty(SerieComprobante) ? SerieComprobante : string.Empty);

    public string Moneda { get; set; } = "PEN";
    public string Estado { get; set; } = string.Empty; // CORRECTO, OBSERVADO, REPROCESAR, ILEGIBLE
    public string? RutaBlobStorage { get; set; }
    public string? NombreArchivo { get; set; }
    public string OrigenDatos { get; set; } = "LOCAL"; // LOCAL | ERP_ANTIGUO
    public DateTime FechaCreacion { get; set; }
    public List<CampoOcrDto> Campos { get; set; } = new();
}


public class DocumentoPaginadoDto
{
    public List<DocumentoDto> Items { get; set; } = new();
    public int TotalRegistros { get; set; }
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
    public int TotalPaginas { get; set; }
}

public class ValidarDocumentoDto
{
    public string Decision { get; set; } = "CORRECTO"; // CORRECTO, OBSERVADO, REPROCESAR, ILEGIBLE
    public string? NuevoEstado { get; set; }
    public string? MotivoObservacion { get; set; }
    public string? Comentarios { get; set; }

    public string ObtenerDecision() => !string.IsNullOrWhiteSpace(NuevoEstado) ? NuevoEstado : Decision;
}

public class CorregirCampoDto
{
    public int IdCampoExtraido { get; set; }
    public string ValorCorregido { get; set; } = string.Empty;
}
