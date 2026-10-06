using System;
using System.Collections.Generic;

namespace Shohin.Application.DTOs.Tickets;

public class TicketDto
{
    public int IdTicket { get; set; }
    public string CodigoTicket { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int TotalDocumentosEsperados { get; set; }
    public int TotalDocumentosProcesados { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string UsuarioCreacion { get; set; } = string.Empty;
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? NumeroCajaArchivador { get; set; }
    public string? RucProveedor { get; set; }
    public string? RazonSocialProveedor { get; set; }
    public string? Prioridad { get; set; }
    public int TotalObservados { get; set; }
    public int TotalCorrectos { get; set; }
}

public class CrearTicketDto
{
    public string? CodigoTicket { get; set; }
    public int TotalDocumentosEsperados { get; set; }
    public string? Observaciones { get; set; }
}

public class ActualizarEstadoTicketDto
{
    public string NuevoEstado { get; set; } = string.Empty; // PENDIENTE, PROCESANDO, PROCESADO, OBSERVADO
    public string? Observaciones { get; set; }
}
