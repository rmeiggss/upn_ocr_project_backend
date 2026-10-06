using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/erp-antiguo")]
public class ErpAntiguoController : ControllerBase
{
    // Catálogo Dummy oficial representativo de la base histórica legacy (SQL Server 2008)
    private static readonly List<DocumentoErpLegacyDto> CatalogoLegacy = new()
    {
        new DocumentoErpLegacyDto
        {
            IdLegacy = 90101,
            RucEmisor = "20100128056",
            RazonSocial = "SERVICIOS GRAFICOS NACIONALES S.A.",
            TipoDocumento = "FACTURA",
            SerieNumero = "F001-00004921",
            FechaEmision = new DateTime(2021, 4, 12),
            Subtotal = 1500.00m,
            Igv = 270.00m,
            Total = 1770.00m,
            Moneda = "PEN",
            EstadoContable = "HISTORICO_CERRADO",
            OrigenSistema = "ERP_ANTIGUO_SQLSERVER_2008"
        },
        new DocumentoErpLegacyDto
        {
            IdLegacy = 90102,
            RucEmisor = "20512345678",
            RazonSocial = "DISTRIBUIDORA INDUSTRIAL DEL PERU S.A.C.",
            TipoDocumento = "FACTURA",
            SerieNumero = "F003-00010482",
            FechaEmision = new DateTime(2020, 9, 25),
            Subtotal = 4200.00m,
            Igv = 756.00m,
            Total = 4956.00m,
            Moneda = "PEN",
            EstadoContable = "HISTORICO_CERRADO",
            OrigenSistema = "ERP_ANTIGUO_SQLSERVER_2008"
        },
        new DocumentoErpLegacyDto
        {
            IdLegacy = 90103,
            RucEmisor = "20498765432",
            RazonSocial = "TRANSPORTES LOGISTICOS DEL SUR S.A.",
            TipoDocumento = "BOLETA",
            SerieNumero = "B001-00000389",
            FechaEmision = new DateTime(2019, 11, 14),
            Subtotal = 650.00m,
            Igv = 117.00m,
            Total = 767.00m,
            Moneda = "PEN",
            EstadoContable = "HISTORICO_CERRADO",
            OrigenSistema = "ERP_ANTIGUO_SQLSERVER_2008"
        },
        new DocumentoErpLegacyDto
        {
            IdLegacy = 90104,
            RucEmisor = "20601234567",
            RazonSocial = "TECNOLOGIA Y REDES GLOBALES S.A.C.",
            TipoDocumento = "NOTA_CREDITO",
            SerieNumero = "NC01-00000120",
            FechaEmision = new DateTime(2021, 8, 30),
            Subtotal = -300.00m,
            Igv = -54.00m,
            Total = -354.00m,
            Moneda = "PEN",
            EstadoContable = "HISTORICO_CERRADO",
            OrigenSistema = "ERP_ANTIGUO_SQLSERVER_2008"
        }
    };

    [HttpGet("documentos")]
    public IActionResult ConsultarDocumentos(
        [FromQuery] string? rucEmisor,
        [FromQuery] string? serieNumero,
        [FromQuery] string? tipoDocumento)
    {
        var query = CatalogoLegacy.AsQueryable();

        if (!string.IsNullOrWhiteSpace(rucEmisor))
            query = query.Where(d => d.RucEmisor.Contains(rucEmisor.Trim()));

        if (!string.IsNullOrWhiteSpace(serieNumero))
            query = query.Where(d => d.SerieNumero.Contains(serieNumero.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(tipoDocumento))
            query = query.Where(d => d.TipoDocumento.Equals(tipoDocumento.Trim(), StringComparison.OrdinalIgnoreCase));

        var resultados = query.ToList();
        return Ok(new
        {
            Exito = true,
            SistemaOrigen = "ERP_ANTIGUO_ACTOR_SECUNDARIO",
            TotalEncontrados = resultados.Count,
            Datos = resultados
        });
    }

    [HttpPost("sincronizar")]
    public IActionResult SincronizarDocumentoValidado([FromBody] SincronizacionErpRequestDto request)
    {
        return Ok(new
        {
            Exito = true,
            CodigoRespuesta = "ERP-SYNC-OK",
            Mensaje = $"Comprobante {request.SerieNumero} sincronizado exitosamente con la tabla HIST_COMPROBANTES del ERP Antiguo.",
            IdRegistroLegacy = new Random().Next(95000, 99999),
            FechaSincronizacion = DateTime.UtcNow
        });
    }

    // Método estático auxiliar para invocación directa interna sin HTTP
    public static List<DocumentoErpLegacyDto> ConsultarInterno(string? rucEmisor, string? serieNumero, string? tipoDocumento)
    {
        var query = CatalogoLegacy.AsQueryable();

        if (!string.IsNullOrWhiteSpace(rucEmisor))
            query = query.Where(d => d.RucEmisor.Contains(rucEmisor.Trim()));

        if (!string.IsNullOrWhiteSpace(serieNumero))
            query = query.Where(d => d.SerieNumero.Contains(serieNumero.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(tipoDocumento))
            query = query.Where(d => d.TipoDocumento.Equals(tipoDocumento.Trim(), StringComparison.OrdinalIgnoreCase));

        return query.ToList();
    }
}

public class DocumentoErpLegacyDto
{
    public int IdLegacy { get; set; }
    public string RucEmisor { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string SerieNumero { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public string Moneda { get; set; } = "PEN";
    public string EstadoContable { get; set; } = string.Empty;
    public string OrigenSistema { get; set; } = string.Empty;
}

public class SincronizacionErpRequestDto
{
    public int IdDocumento { get; set; }
    public string SerieNumero { get; set; } = string.Empty;
    public string RucEmisor { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
