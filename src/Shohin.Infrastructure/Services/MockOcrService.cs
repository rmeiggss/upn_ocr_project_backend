using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

public class MockOcrService : IOcrService
{
    private record DocData(string Tipo, string Ruc, string RazonSocial, decimal Subtotal, decimal Igv, decimal Total, string Moneda, bool EsObservado);

    private static readonly Dictionary<string, DocData> ManifestCache = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FC02-00000598"] = new DocData("NOTA_CREDITO", "20505874211", "SUMINISTROS INDUSTRIALES DEL NORTE E.I.R.L.", 2034.83m, 366.27m, 2401.1m, "PEN", false),
        ["F003-00000599"] = new DocData("FACTURA", "20607891234", "TRANSPORTES Y CARGA PESADA EXPRES DEL SUR S.A.C.", 11084.62m, 1995.23m, 13079.85m, "PEN", false),
        ["F002-00000600"] = new DocData("FACTURA", "20100128056", "CORPORACION LOGISTICA INTEGRAL S.A.C.", 781.22m, 140.62m, 921.84m, "PEN", false),
        ["F003-00000601"] = new DocData("FACTURA", "20609876541", "ENERGIA Y CLIMATIZACION GLOBAL S.A.C.", 5703.18m, 1026.57m, 6729.75m, "PEN", false),
        ["B002-00000602"] = new DocData("BOLETA", "20334455667", "IMPORTADORA Y EXPORTADORA ANDINA S.A.C.", 12783.04m, 2300.95m, 15083.99m, "PEN", true),
        ["F002-00000603"] = new DocData("FACTURA", "20498765432", "TEXTILES & CONFECCIONES DEL VALLE S.A.C.", 579.62m, 104.33m, 683.95m, "PEN", false),
        ["F002-00000604"] = new DocData("FACTURA", "20543219876", "SEGURIDAD Y VIGILANCIA INTEGRAL S.A.C.", 3913.7m, 704.47m, 4618.17m, "PEN", false),
        ["F001-00000605"] = new DocData("FACTURA", "20505874211", "SUMINISTROS INDUSTRIALES DEL NORTE E.I.R.L.", 13656.94m, 2458.25m, 16115.19m, "PEN", false),
        ["F003-00000606"] = new DocData("FACTURA", "20100128056", "CORPORACION LOGISTICA INTEGRAL S.A.C.", 667.28m, 120.11m, 787.39m, "PEN", false),
        ["FC01-00000607"] = new DocData("NOTA_CREDITO", "20609876541", "ENERGIA Y CLIMATIZACION GLOBAL S.A.C.", 8456.9m, 1522.24m, 9979.14m, "PEN", false),
        ["FC02-00000608"] = new DocData("NOTA_CREDITO", "20601248593", "SERVICIOS INFORMATICOS & REDES S.A.", 142.47m, 25.64m, 168.11m, "PEN", false),
        ["F004-00000609"] = new DocData("FACTURA", "20100128056", "CORPORACION LOGISTICA INTEGRAL S.A.C.", 199.04m, 29.86m, 228.9m, "PEN", true),
        ["F003-00000610"] = new DocData("FACTURA", "20489632145", "DISTRIBUIDORA COMERCIAL LIMA SUR S.A.", 511.69m, 76.75m, 588.44m, "PEN", true),
        ["B001-00000611"] = new DocData("BOLETA", "20601248593", "SERVICIOS INFORMATICOS & REDES S.A.", 634.15m, 114.15m, 748.3m, "PEN", true),
        ["FC01-00000612"] = new DocData("NOTA_CREDITO", "20456123789", "GRUPO ALIMENTARIO DEL CENTRO S.A.", 222.44m, 40.04m, 262.48m, "USD", true),
        ["F003-00000613"] = new DocData("FACTURA", "20556789123", "TECNOLOGIA Y SISTEMAS DEL PERU S.A.C.", 518.35m, 93.3m, 611.65m, "PEN", false),
        ["F004-00000614"] = new DocData("FACTURA", "20543219876", "SEGURIDAD Y VIGILANCIA INTEGRAL S.A.C.", 674.88m, 121.48m, 796.36m, "PEN", true),
        ["F003-00000615"] = new DocData("FACTURA", "20607891234", "TRANSPORTES Y CARGA PESADA EXPRES DEL SUR S.A.C.", 2187.37m, 393.73m, 2581.1m, "PEN", false),
        ["F004-00000616"] = new DocData("FACTURA", "20543219876", "SEGURIDAD Y VIGILANCIA INTEGRAL S.A.C.", 3940.34m, 709.26m, 4649.6m, "PEN", false),
        ["F004-00000617"] = new DocData("FACTURA", "20498765432", "TEXTILES & CONFECCIONES DEL VALLE S.A.C.", 1329.92m, 239.39m, 1569.31m, "PEN", false),
        ["F003-00000618"] = new DocData("FACTURA", "20512894563", "CONSTRUCTORA E INMOBILIARIA HORIZONTE S.A.C.", 46.4m, 8.35m, 54.75m, "PEN", false),
        ["B003-00000619"] = new DocData("BOLETA", "20603456781", "SOLUCIONES METALMECANICAS LIMA E.I.R.L.", 2342.45m, 421.64m, 2764.09m, "PEN", false),
        ["FC01-00000620"] = new DocData("NOTA_CREDITO", "20334455667", "IMPORTADORA Y EXPORTADORA ANDINA S.A.C.", 9740.8m, 1753.34m, 11494.14m, "PEN", false),
        ["F004-00000621"] = new DocData("FACTURA", "20345671239", "CONSORCIO GRAFICO & EDITORIAL LIMA S.A.", 1845.84m, 332.25m, 2178.09m, "PEN", true),
        ["F004-00000622"] = new DocData("FACTURA", "20345671239", "CONSORCIO GRAFICO & EDITORIAL LIMA S.A.", 3991.89m, 718.54m, 4710.43m, "PEN", false),
        ["F004-00000623"] = new DocData("FACTURA", "20603456781", "SOLUCIONES METALMECANICAS LIMA E.I.R.L.", 109.76m, 19.76m, 129.52m, "PEN", false),
        ["F002-00000624"] = new DocData("FACTURA", "20345671239", "CONSORCIO GRAFICO & EDITORIAL LIMA S.A.", 104.88m, 18.88m, 123.76m, "PEN", false),
        ["F003-00000625"] = new DocData("FACTURA", "20505874211", "SUMINISTROS INDUSTRIALES DEL NORTE E.I.R.L.", 541.28m, 97.43m, 638.71m, "PEN", false),
        ["FC02-00000626"] = new DocData("NOTA_CREDITO", "20498765432", "TEXTILES & CONFECCIONES DEL VALLE S.A.C.", 25.38m, 4.57m, 29.95m, "PEN", false),
        ["FC02-00000627"] = new DocData("NOTA_CREDITO", "20489632145", "DISTRIBUIDORA COMERCIAL LIMA SUR S.A.", 772.62m, 139.07m, 911.69m, "PEN", false),
        ["B002-00000628"] = new DocData("BOLETA", "20456123789", "GRUPO ALIMENTARIO DEL CENTRO S.A.", 12768.25m, 2298.29m, 15066.54m, "PEN", false),
        ["F002-00000629"] = new DocData("FACTURA", "20601248593", "SERVICIOS INFORMATICOS & REDES S.A.", 15796.95m, 2843.45m, 18640.4m, "PEN", false),
        ["B001-00000630"] = new DocData("BOLETA", "20489632145", "DISTRIBUIDORA COMERCIAL LIMA SUR S.A.", 8984.05m, 1617.13m, 10601.18m, "PEN", false),
        ["F004-00000631"] = new DocData("FACTURA", "20609876541", "ENERGIA Y CLIMATIZACION GLOBAL S.A.C.", 43.44m, 7.82m, 51.26m, "PEN", false),
        ["B003-00000632"] = new DocData("BOLETA", "20543219876", "SEGURIDAD Y VIGILANCIA INTEGRAL S.A.C.", 460.24m, 82.84m, 543.08m, "PEN", false),
        ["F002-00000633"] = new DocData("FACTURA", "20456123789", "GRUPO ALIMENTARIO DEL CENTRO S.A.", 100.92m, 15.14m, 116.06m, "PEN", true),
        ["FC02-00000634"] = new DocData("NOTA_CREDITO", "20334455667", "IMPORTADORA Y EXPORTADORA ANDINA S.A.C.", 10785.44m, 1941.38m, 12726.82m, "PEN", false),
        ["B002-00000635"] = new DocData("BOLETA", "20603456781", "SOLUCIONES METALMECANICAS LIMA E.I.R.L.", 2343.46m, 351.52m, 2694.98m, "PEN", true),
        ["F004-00000636"] = new DocData("FACTURA", "20523456128", "LABORATORIOS QUIMICOS INDUSTRIALES DEL PACIFICO S.A.C.", 552.04m, 99.37m, 651.41m, "USD", false),
        ["F001-00000637"] = new DocData("FACTURA", "20609876541", "ENERGIA Y CLIMATIZACION GLOBAL S.A.C.", 10831.29m, 1949.63m, 12780.92m, "PEN", false),
        ["B003-00000638"] = new DocData("BOLETA", "20523456128", "LABORATORIOS QUIMICOS INDUSTRIALES DEL PACIFICO S.A.C.", 2220.21m, 399.64m, 2619.85m, "PEN", false),
        ["F004-00000639"] = new DocData("FACTURA", "20489632145", "DISTRIBUIDORA COMERCIAL LIMA SUR S.A.", 3472.6m, 520.89m, 3993.49m, "PEN", true),
        ["F001-00000640"] = new DocData("FACTURA", "20601248593", "SERVICIOS INFORMATICOS & REDES S.A.", 769.2m, 138.46m, 907.66m, "PEN", false),
        ["F001-00000641"] = new DocData("FACTURA", "20345671239", "CONSORCIO GRAFICO & EDITORIAL LIMA S.A.", 246.37m, 44.35m, 290.72m, "PEN", false),
        ["B001-00000642"] = new DocData("BOLETA", "20609876541", "ENERGIA Y CLIMATIZACION GLOBAL S.A.C.", 10149.44m, 1826.9m, 11976.34m, "PEN", true),
        ["B001-00000643"] = new DocData("BOLETA", "20512894563", "CONSTRUCTORA E INMOBILIARIA HORIZONTE S.A.C.", 13009.96m, 2341.79m, 15351.75m, "PEN", false),
        ["F004-00000644"] = new DocData("FACTURA", "20512894563", "CONSTRUCTORA E INMOBILIARIA HORIZONTE S.A.C.", 782.84m, 140.91m, 923.75m, "PEN", false),
        ["F002-00000645"] = new DocData("FACTURA", "20607891234", "TRANSPORTES Y CARGA PESADA EXPRES DEL SUR S.A.C.", 133.05m, 23.95m, 157.0m, "PEN", false),
        ["B001-00000646"] = new DocData("BOLETA", "20609876541", "ENERGIA Y CLIMATIZACION GLOBAL S.A.C.", 73.56m, 11.03m, 84.59m, "USD", true),
        ["B003-00000647"] = new DocData("BOLETA", "20456123789", "GRUPO ALIMENTARIO DEL CENTRO S.A.", 4006.08m, 721.09m, 4727.17m, "PEN", false),
    };

    public Task<OcrDocumentoResult> ExtraerDatosDocumentoAsync(Stream archivoStream, string nombreArchivo)
    {
        // 1. Extraer serie y número del nombre de archivo (ej: 20261002_071748_BOLETA_B001_00000630.pdf o FACTURA_F001_00000640.pdf)
        string? serie = null;
        string? numero = null;
        string? key = null;

        var snMatch = Regex.Match(nombreArchivo, @"([FB][0-9A-Z]{3}|FC\d{2})[-_](\d{6,8})", RegexOptions.IgnoreCase);
        if (snMatch.Success)
        {
            serie = snMatch.Groups[1].Value.ToUpperInvariant();
            numero = snMatch.Groups[2].Value;
            key = $"{serie}-{numero}";
        }

        bool esObsNombre = nombreArchivo.Contains("obs", StringComparison.OrdinalIgnoreCase) ||
                           nombreArchivo.Contains("duda", StringComparison.OrdinalIgnoreCase);

        if (key != null && ManifestCache.TryGetValue(key, out var data))
        {
            bool esObservado = data.EsObservado || esObsNombre;
            var confianzaGeneral = esObservado ? 74.50m : 98.50m;
            var confianzaIgv = esObservado ? 64.20m : 97.80m;
            var fecha = DateTime.UtcNow.AddDays(-15);

            return Task.FromResult(new OcrDocumentoResult
            {
                RucEmisor = data.Ruc,
                RazonSocial = data.RazonSocial,
                SerieComprobante = serie,
                NumeroComprobante = numero,
                FechaEmision = fecha,
                MontoSubTotal = data.Subtotal,
                MontoIgv = data.Igv,
                MontoTotal = data.Total,
                Moneda = data.Moneda,
                TipoDocumentoSugerido = data.Tipo,
                ConfianzaGeneral = confianzaGeneral,
                Campos = new List<OcrCampoExtraidoResult>
                {
                    new() { NombreCampo = "RucEmisor", Valor = data.Ruc, NivelConfianza = 99.50m },
                    new() { NombreCampo = "RazonSocial", Valor = data.RazonSocial, NivelConfianza = 98.10m },
                    new() { NombreCampo = "SerieComprobante", Valor = serie, NivelConfianza = 99.00m },
                    new() { NombreCampo = "NumeroComprobante", Valor = numero, NivelConfianza = 98.70m },
                    new() { NombreCampo = "FechaEmision", Valor = fecha.ToString("yyyy-MM-dd"), NivelConfianza = 96.50m },
                    new() { NombreCampo = "MontoSubTotal", Valor = data.Subtotal.ToString("F2"), NivelConfianza = 95.00m },
                    new() { NombreCampo = "MontoIgv", Valor = data.Igv.ToString("F2"), NivelConfianza = confianzaIgv },
                    new() { NombreCampo = "MontoTotal", Valor = data.Total.ToString("F2"), NivelConfianza = 99.20m }
                }
            });
        }

        // Si no está en el catálogo, fallback determinista basado en el archivo
        var tipoSugerido = "FACTURA";
        if (nombreArchivo.Contains("boleta", StringComparison.OrdinalIgnoreCase)) tipoSugerido = "BOLETA";
        else if (nombreArchivo.Contains("credito", StringComparison.OrdinalIgnoreCase) || nombreArchivo.Contains("fc", StringComparison.OrdinalIgnoreCase)) tipoSugerido = "NOTA_CREDITO";

        serie ??= tipoSugerido == "BOLETA" ? "B001" : (tipoSugerido == "NOTA_CREDITO" ? "FC01" : "F001");
        numero ??= "00000001";

        var confG = esObsNombre ? 74.50m : 98.50m;
        var confI = esObsNombre ? 64.20m : 97.80m;
        var fEmision = DateTime.UtcNow.AddDays(-10);
        var sub = 1000.00m;
        var igv = 180.00m;
        var tot = 1180.00m;

        return Task.FromResult(new OcrDocumentoResult
        {
            RucEmisor = "20100128056",
            RazonSocial = "CORPORACION LOGISTICA INTEGRAL S.A.C.",
            SerieComprobante = serie,
            NumeroComprobante = numero,
            FechaEmision = fEmision,
            MontoSubTotal = sub,
            MontoIgv = igv,
            MontoTotal = tot,
            Moneda = "PEN",
            TipoDocumentoSugerido = tipoSugerido,
            ConfianzaGeneral = confG,
            Campos = new List<OcrCampoExtraidoResult>
            {
                new() { NombreCampo = "RucEmisor", Valor = "20100128056", NivelConfianza = 99.50m },
                new() { NombreCampo = "RazonSocial", Valor = "CORPORACION LOGISTICA INTEGRAL S.A.C.", NivelConfianza = 98.10m },
                new() { NombreCampo = "SerieComprobante", Valor = serie, NivelConfianza = 99.00m },
                new() { NombreCampo = "NumeroComprobante", Valor = numero, NivelConfianza = 98.70m },
                new() { NombreCampo = "FechaEmision", Valor = fEmision.ToString("yyyy-MM-dd"), NivelConfianza = 96.50m },
                new() { NombreCampo = "MontoSubTotal", Valor = sub.ToString("F2"), NivelConfianza = 95.00m },
                new() { NombreCampo = "MontoIgv", Valor = igv.ToString("F2"), NivelConfianza = confI },
                new() { NombreCampo = "MontoTotal", Valor = tot.ToString("F2"), NivelConfianza = 99.20m }
            }
        });
    }
}
