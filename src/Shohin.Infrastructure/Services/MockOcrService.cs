using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

public class MockOcrService : IOcrService
{
    private static readonly Random _random = new();

    public Task<OcrDocumentoResult> ExtraerDatosDocumentoAsync(Stream archivoStream, string nombreArchivo)
    {
        // Heurística de simulación inteligente para desarrollo y sustentación
        var esObservado = nombreArchivo.Contains("obs", StringComparison.OrdinalIgnoreCase) ||
                          nombreArchivo.Contains("duda", StringComparison.OrdinalIgnoreCase);

        var rucs = new[] { "20100128056", "20505874211", "20601248593", "20489632145", "20334455667" };
        var razones = new[]
        {
            "CORPORACION LOGISTICA INTEGRAL S.A.C.",
            "SUMINISTROS INDUSTRIALES DEL NORTE E.I.R.L.",
            "SERVICIOS INFORMATICOS & REDES S.A.",
            "DISTRIBUIDORA COMERCIAL LIMA SUR S.A.",
            "IMPORTADORA Y EXPORTADORA ANDINA S.A.C."
        };

        var index = Math.Abs(nombreArchivo.GetHashCode()) % rucs.Length;
        var ruc = rucs[index];
        var razon = razones[index];

        var serie = "F" + _random.Next(1, 9).ToString("D3");
        var numero = _random.Next(1000, 99999).ToString("D8");
        var fecha = DateTime.UtcNow.AddDays(-_random.Next(1, 30));

        var subtotal = Math.Round((decimal)(_random.Next(500, 5000) + _random.NextDouble()), 2);
        var igv = Math.Round(subtotal * 0.18m, 2);
        var total = subtotal + igv;

        // Niveles de confianza
        var confianzaGeneral = esObservado ? 74.50m : 98.20m;
        var confianzaIgv = esObservado ? 64.20m : 97.80m; // Dispara CUS-05 si es observado

        var resultado = new OcrDocumentoResult
        {
            RucEmisor = ruc,
            RazonSocial = razon,
            SerieComprobante = serie,
            NumeroComprobante = numero,
            FechaEmision = fecha,
            MontoSubTotal = subtotal,
            MontoIgv = igv,
            MontoTotal = total,
            Moneda = "PEN",
            TipoDocumentoSugerido = "FACTURA",
            ConfianzaGeneral = confianzaGeneral,
            Campos = new List<OcrCampoExtraidoResult>
            {
                new() { NombreCampo = "RucEmisor", Valor = ruc, NivelConfianza = 99.50m },
                new() { NombreCampo = "RazonSocial", Valor = razon, NivelConfianza = 98.10m },
                new() { NombreCampo = "SerieComprobante", Valor = serie, NivelConfianza = 99.00m },
                new() { NombreCampo = "NumeroComprobante", Valor = numero, NivelConfianza = 98.70m },
                new() { NombreCampo = "FechaEmision", Valor = fecha.ToString("yyyy-MM-dd"), NivelConfianza = 96.50m },
                new() { NombreCampo = "MontoSubTotal", Valor = subtotal.ToString("F2"), NivelConfianza = 95.00m },
                new() { NombreCampo = "MontoIgv", Valor = igv.ToString("F2"), NivelConfianza = confianzaIgv },
                new() { NombreCampo = "MontoTotal", Valor = total.ToString("F2"), NivelConfianza = 99.20m }
            }
        };

        return Task.FromResult(resultado);
    }
}
