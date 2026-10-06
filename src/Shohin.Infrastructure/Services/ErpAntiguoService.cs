using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shohin.Application.DTOs.Documentos;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

public class ErpAntiguoService : IErpAntiguoService
{
    private static readonly List<DocumentoDto> CatalogoLegacy = new()
    {
        new DocumentoDto
        {
            IdDocumento = 90101,
            RucEmisor = "20100128056",
            RazonSocial = "SERVICIOS GRAFICOS NACIONALES S.A.",
            TipoDocumento = "FACTURA",
            SerieComprobante = "F001",
            NumeroComprobante = "00004921",
            FechaEmision = new DateTime(2021, 4, 12),
            MontoSubTotal = 1500.00m,
            MontoIgv = 270.00m,
            MontoTotal = 1770.00m,
            Moneda = "PEN",
            Estado = "CORRECTO",
            OrigenDatos = "ERP_ANTIGUO",
            FechaCreacion = new DateTime(2021, 4, 12)
        },
        new DocumentoDto
        {
            IdDocumento = 90102,
            RucEmisor = "20512345678",
            RazonSocial = "DISTRIBUIDORA INDUSTRIAL DEL PERU S.A.C.",
            TipoDocumento = "FACTURA",
            SerieComprobante = "F003",
            NumeroComprobante = "00010482",
            FechaEmision = new DateTime(2020, 9, 25),
            MontoSubTotal = 4200.00m,
            MontoIgv = 756.00m,
            MontoTotal = 4956.00m,
            Moneda = "PEN",
            Estado = "CORRECTO",
            OrigenDatos = "ERP_ANTIGUO",
            FechaCreacion = new DateTime(2020, 9, 25)
        },
        new DocumentoDto
        {
            IdDocumento = 90103,
            RucEmisor = "20498765432",
            RazonSocial = "TRANSPORTES LOGISTICOS DEL SUR S.A.",
            TipoDocumento = "BOLETA",
            SerieComprobante = "B001",
            NumeroComprobante = "00000389",
            FechaEmision = new DateTime(2019, 11, 14),
            MontoSubTotal = 650.00m,
            MontoIgv = 117.00m,
            MontoTotal = 767.00m,
            Moneda = "PEN",
            Estado = "CORRECTO",
            OrigenDatos = "ERP_ANTIGUO",
            FechaCreacion = new DateTime(2019, 11, 14)
        },
        new DocumentoDto
        {
            IdDocumento = 90104,
            RucEmisor = "20601234567",
            RazonSocial = "TECNOLOGIA Y REDES GLOBALES S.A.C.",
            TipoDocumento = "NOTA_CREDITO",
            SerieComprobante = "NC01",
            NumeroComprobante = "00000120",
            FechaEmision = new DateTime(2021, 8, 30),
            MontoSubTotal = -300.00m,
            MontoIgv = -54.00m,
            MontoTotal = -354.00m,
            Moneda = "PEN",
            Estado = "CORRECTO",
            OrigenDatos = "ERP_ANTIGUO",
            FechaCreacion = new DateTime(2021, 8, 30)
        }
    };

    public Task<List<DocumentoDto>> ConsultarDocumentosLegacyAsync(string? rucEmisor, string? serieNumero, string? tipoDocumento)
    {
        var query = CatalogoLegacy.AsQueryable();

        if (!string.IsNullOrWhiteSpace(rucEmisor))
            query = query.Where(d => d.RucEmisor != null && d.RucEmisor.Contains(rucEmisor.Trim()));

        if (!string.IsNullOrWhiteSpace(serieNumero))
        {
            var serieLimpia = serieNumero.Trim().Replace("-", "").ToLower();
            query = query.Where(d => 
                (d.SerieComprobante != null && d.NumeroComprobante != null && 
                 $"{d.SerieComprobante}{d.NumeroComprobante}".ToLower().Contains(serieLimpia)) ||
                (d.SerieComprobante != null && d.SerieComprobante.ToLower().Contains(serieLimpia)) ||
                (d.NumeroComprobante != null && d.NumeroComprobante.ToLower().Contains(serieLimpia)));
        }

        if (!string.IsNullOrWhiteSpace(tipoDocumento))
            query = query.Where(d => d.TipoDocumento.Equals(tipoDocumento.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(query.ToList());
    }
}
