using System;
using System.Collections.Generic;

namespace Shohin.Application.DTOs.Historico;

public class ConsultaHistoricoFiltroDto
{
    public string? RucEmisor { get; set; }
    public string? RazonSocial { get; set; }
    public string? TipoDocumento { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public string? CodigoTicket { get; set; }
    public int Pagina { get; set; } = 1;
    public int RegistrosPorPagina { get; set; } = 10;
}

public class SolicitudBusquedaFisicaDto
{
    public string RucEmisor { get; set; } = string.Empty;
    public string? SerieNumero { get; set; }
    public string? AnioPeriodo { get; set; }
    public string MotivoSolicitud { get; set; } = string.Empty;
}
