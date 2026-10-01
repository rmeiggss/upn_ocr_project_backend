using System;
using System.Collections.Generic;

namespace Shohin.Application.DTOs.Reportes;

public class ReporteFiltroDto
{
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? TipoDocumento { get; set; }
    public string? RucEmisor { get; set; }
}

public class ItemReporteAuditoriaDto
{
    public string CodigoTicket { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string RucEmisor { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string SerieNumero { get; set; } = string.Empty;
    public DateTime? FechaEmision { get; set; }
    public decimal MontoTotal { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string HashIntegridad { get; set; } = string.Empty;
    public decimal PromedioConfianzaOcr { get; set; }
}

public class ResumenReporteAuditoriaDto
{
    public int TotalDocumentos { get; set; }
    public decimal MontoAcumulado { get; set; }
    public int TotalCorrectos { get; set; }
    public int TotalObservados { get; set; }
    public decimal PorcentajeConfiabilidad { get; set; }
    public List<ItemReporteAuditoriaDto> Items { get; set; } = new();
}

public class ParametroDto
{
    public int IdParametro { get; set; }
    public string Grupo { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
