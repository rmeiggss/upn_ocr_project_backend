using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shohin.Application.Interfaces;

namespace Shohin.Infrastructure.Services;

/// <summary>
/// Implementación Cloud-First de IOcrService utilizando Azure AI Document Intelligence
/// (anteriormente Azure Form Recognizer) con el modelo preentrenado 'prebuilt-invoice'.
/// </summary>
public class AzureDocumentIntelligenceService : IOcrService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureDocumentIntelligenceService> _logger;
    private readonly MockOcrService _fallbackService;
    private readonly DocumentAnalysisClient? _client;

    public AzureDocumentIntelligenceService(
        IConfiguration configuration,
        ILogger<AzureDocumentIntelligenceService> logger,
        MockOcrService fallbackService)
    {
        _configuration = configuration;
        _logger = logger;
        _fallbackService = fallbackService;

        var endpoint = _configuration["Ocr:AzureEndpoint"] ??
                       _configuration["Ocr__AzureEndpoint"] ??
                       _configuration["DocumentIntelligence:Endpoint"];

        var apiKey = _configuration["Ocr:AzureApiKey"] ??
                     _configuration["Ocr__AzureApiKey"] ??
                     _configuration["DocumentIntelligence:ApiKey"];

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                _logger.LogInformation("🤖 Azure AI Document Intelligence inicializado con éxito. Endpoint: {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo inicializar DocumentAnalysisClient.");
            }
        }
        else
        {
            _logger.LogWarning("⚠️ Credenciales de Azure Document Intelligence incompletas en la configuración.");
        }
    }

    public async Task<OcrDocumentoResult> ExtraerDatosDocumentoAsync(Stream archivoStream, string nombreArchivo)
    {
        if (_client == null)
        {
            _logger.LogWarning("⚠️ DocumentAnalysisClient no disponible. Delegando extracción a MockOcrService...");
            return await _fallbackService.ExtraerDatosDocumentoAsync(archivoStream, nombreArchivo);
        }

        try
        {
            _logger.LogInformation("🚀 [Azure Document Intelligence] Analizando '{Nombre}' con modelo 'prebuilt-invoice'...", nombreArchivo);

            if (archivoStream.CanSeek)
            {
                archivoStream.Position = 0;
            }

            AnalyzeDocumentOperation operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-invoice",
                archivoStream);

            AnalyzeResult result = operation.Value;
            var analyzedDoc = result.Documents.FirstOrDefault();

            if (analyzedDoc == null)
            {
                _logger.LogWarning("⚠️ Azure Document Intelligence no detectó estructura de documento contable en '{Nombre}'.", nombreArchivo);
                return await _fallbackService.ExtraerDatosDocumentoAsync(archivoStream, nombreArchivo);
            }

            // 1. RUC Emisor (VendorTaxId)
            string? ruc = null;
            decimal rucConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("VendorTaxId", out var vendorTaxField))
            {
                ruc = Regex.Replace(vendorTaxField.Content ?? "", @"[^\d]", "");
                rucConf = Math.Round((decimal)(vendorTaxField.Confidence ?? 0.95f) * 100m, 2);
            }

            // 2. Razón Social (VendorName)
            string? razonSocial = null;
            decimal razonConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("VendorName", out var vendorNameField))
            {
                razonSocial = vendorNameField.Content?.Trim();
                razonConf = Math.Round((decimal)(vendorNameField.Confidence ?? 0.95f) * 100m, 2);
            }

            // 3. Serie y Número (InvoiceId)
            string? serie = null;
            string? numero = null;
            decimal snConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("InvoiceId", out var invoiceIdField))
            {
                var invoiceIdContent = invoiceIdField.Content?.Trim();
                snConf = Math.Round((decimal)(invoiceIdField.Confidence ?? 0.95f) * 100m, 2);
                if (!string.IsNullOrEmpty(invoiceIdContent))
                {
                    var partes = invoiceIdContent.Split('-');
                    if (partes.Length >= 2)
                    {
                        serie = partes[0].Trim().ToUpperInvariant();
                        numero = partes[1].Trim();
                    }
                    else
                    {
                        var regexMatch = Regex.Match(invoiceIdContent, @"([FB][0-9A-Z]{3}|FC\d{2})[-_]?(\d{6,8})", RegexOptions.IgnoreCase);
                        if (regexMatch.Success)
                        {
                            serie = regexMatch.Groups[1].Value.ToUpperInvariant();
                            numero = regexMatch.Groups[2].Value;
                        }
                    }
                }
            }

            // Fallback de serie/número por nombre de archivo si el OCR vino difuso
            if (string.IsNullOrEmpty(serie) || string.IsNullOrEmpty(numero))
            {
                var snMatchFile = Regex.Match(nombreArchivo, @"([FB][0-9A-Z]{3}|FC\d{2})[-_](\d{6,8})", RegexOptions.IgnoreCase);
                if (snMatchFile.Success)
                {
                    serie ??= snMatchFile.Groups[1].Value.ToUpperInvariant();
                    numero ??= snMatchFile.Groups[2].Value;
                }
            }

            // 4. Fecha de Emisión (InvoiceDate)
            DateTime? fechaEmision = null;
            decimal fechaConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("InvoiceDate", out var invoiceDateField))
            {
                fechaConf = Math.Round((decimal)(invoiceDateField.Confidence ?? 0.95f) * 100m, 2);
                if (DateTime.TryParse(invoiceDateField.Content, new CultureInfo("es-PE"), DateTimeStyles.None, out var dt) ||
                    DateTime.TryParse(invoiceDateField.Content, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    fechaEmision = dt;
                }
            }

            // 5. Montos: Subtotal, IGV y Total
            decimal? subtotal = null;
            decimal subtotalConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("SubTotal", out var subTotalField))
            {
                subtotal = ExtraerMonto(subTotalField.Content);
                subtotalConf = Math.Round((decimal)(subTotalField.Confidence ?? 0.95f) * 100m, 2);
            }

            decimal? igv = null;
            decimal igvConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("TotalTax", out var totalTaxField))
            {
                igv = ExtraerMonto(totalTaxField.Content);
                igvConf = Math.Round((decimal)(totalTaxField.Confidence ?? 0.95f) * 100m, 2);
            }

            decimal? total = null;
            decimal totalConf = 95.00m;
            if (analyzedDoc.Fields.TryGetValue("InvoiceTotal", out var invoiceTotalField))
            {
                total = ExtraerMonto(invoiceTotalField.Content);
                totalConf = Math.Round((decimal)(invoiceTotalField.Confidence ?? 0.95f) * 100m, 2);
            }

            // Tipo de Documento Sugerido
            var tipoDocumento = "FACTURA";
            if ((serie != null && serie.StartsWith("B", StringComparison.OrdinalIgnoreCase)) ||
                nombreArchivo.Contains("boleta", StringComparison.OrdinalIgnoreCase))
            {
                tipoDocumento = "BOLETA";
            }
            else if ((serie != null && serie.StartsWith("FC", StringComparison.OrdinalIgnoreCase)) ||
                     nombreArchivo.Contains("credito", StringComparison.OrdinalIgnoreCase) ||
                     nombreArchivo.Contains("fc", StringComparison.OrdinalIgnoreCase))
            {
                tipoDocumento = "NOTA_CREDITO";
            }

            // Cálculo de Confianza General
            var confianzas = new List<decimal> { rucConf, razonConf, snConf, fechaConf, subtotalConf, igvConf, totalConf };
            var confianzaGeneral = Math.Round(confianzas.Average(), 2);

            var camposList = new List<OcrCampoExtraidoResult>
            {
                new() { NombreCampo = "RucEmisor", Valor = ruc, NivelConfianza = rucConf },
                new() { NombreCampo = "RazonSocial", Valor = razonSocial, NivelConfianza = razonConf },
                new() { NombreCampo = "SerieComprobante", Valor = serie, NivelConfianza = snConf },
                new() { NombreCampo = "NumeroComprobante", Valor = numero, NivelConfianza = snConf },
                new() { NombreCampo = "FechaEmision", Valor = fechaEmision?.ToString("yyyy-MM-dd"), NivelConfianza = fechaConf },
                new() { NombreCampo = "MontoSubTotal", Valor = subtotal?.ToString("F2", CultureInfo.InvariantCulture), NivelConfianza = subtotalConf },
                new() { NombreCampo = "MontoIgv", Valor = igv?.ToString("F2", CultureInfo.InvariantCulture), NivelConfianza = igvConf },
                new() { NombreCampo = "MontoTotal", Valor = total?.ToString("F2", CultureInfo.InvariantCulture), NivelConfianza = totalConf }
            };

            _logger.LogInformation("✅ [Azure Document Intelligence] Procesado '{Nombre}': Tipo={Tipo}, {Serie}-{Num}, RUC={Ruc}, Total={Total}, Conf={Conf}%",
                nombreArchivo, tipoDocumento, serie, numero, ruc, total, confianzaGeneral);

            return new OcrDocumentoResult
            {
                RucEmisor = ruc,
                RazonSocial = razonSocial,
                SerieComprobante = serie,
                NumeroComprobante = numero,
                FechaEmision = fechaEmision,
                MontoSubTotal = subtotal,
                MontoIgv = igv,
                MontoTotal = total,
                Moneda = "PEN",
                TipoDocumentoSugerido = tipoDocumento,
                ConfianzaGeneral = confianzaGeneral,
                Campos = camposList
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error durante la invocación de Azure Document Intelligence para '{Nombre}'. Ejecutando fallback...", nombreArchivo);
            return await _fallbackService.ExtraerDatosDocumentoAsync(archivoStream, nombreArchivo);
        }
    }

    private static decimal? ExtraerMonto(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        // Limpiar símbolos de moneda y caracteres no numéricos excepto coma y punto
        var cleaned = Regex.Replace(content, @"[^\d.,]", "").Trim();

        // Si contiene coma y punto (ej. 1,234.56 o 1.234,56)
        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            if (cleaned.IndexOf(',') < cleaned.IndexOf('.'))
            {
                cleaned = cleaned.Replace(",", "");
            }
            else
            {
                cleaned = cleaned.Replace(".", "").Replace(",", ".");
            }
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(",", ".");
        }

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        return null;
    }
}
