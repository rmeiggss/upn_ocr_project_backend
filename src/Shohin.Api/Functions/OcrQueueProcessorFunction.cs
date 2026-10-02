using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

/// <summary>
/// Función Serverless Cloud-First:
/// Se engatilla automáticamente cuando un archivo PDF ingresa a Azure Blob Storage
/// y Event Grid emite el evento 'Microsoft.Storage.BlobCreated' hacia la cola 'cola-digitalizacion-ocr'.
/// </summary>
public class OcrQueueProcessorFunction
{
    private readonly DigitalizacionService _digitalizacionService;
    private readonly ILogger<OcrQueueProcessorFunction> _logger;

    public OcrQueueProcessorFunction(
        DigitalizacionService digitalizacionService,
        ILogger<OcrQueueProcessorFunction> logger)
    {
        _digitalizacionService = digitalizacionService;
        _logger = logger;
    }

    [Function("Ocr_ProcesarEventoBlobEncolado")]
    public async Task Run(
        [QueueTrigger("cola-digitalizacion-ocr")] string messageText)
    {
        _logger.LogInformation("⚡ [Cloud Trigger] Evento de digitalización recibido de la cola: {Msg}", messageText);

        string? rutaBlob = null;

        // Intentar parsear el mensaje Event Grid JSON
        try
        {
            using var doc = JsonDocument.Parse(messageText);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataElem) && dataElem.TryGetProperty("url", out var urlElem))
            {
                rutaBlob = urlElem.GetString();
            }
            else if (root.TryGetProperty("subject", out var subjectElem))
            {
                rutaBlob = subjectElem.GetString();
            }
        }
        catch
        {
            // Si el mensaje en la cola es directamente la URL o ruta del blob
            rutaBlob = messageText.Trim();
        }

        if (string.IsNullOrWhiteSpace(rutaBlob))
        {
            _logger.LogWarning("⚠️ No se pudo determinar la ruta del blob desde el mensaje de la cola.");
            return;
        }

        _logger.LogInformation("🤖 Iniciando extracción OCR en la nube para blob: {Ruta}", rutaBlob);

        var result = await _digitalizacionService.ProcesarDocumentoPorEventoOcrAsync(rutaBlob);

        if (result.Exito)
        {
            _logger.LogInformation("✅ OCR completado exitosamente en la nube. Doc ID: {Id}, Estado: {Estado}",
                result.Datos?.IdDocumento, result.Datos?.Estado);
        }
        else
        {
            _logger.LogError("❌ Error durante el procesamiento OCR en la nube: {Msg}", result.Mensaje);
            throw new Exception($"Fallo en procesamiento de OCR para {rutaBlob}: {result.Mensaje}");
        }
    }
}
