using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Documentos;
using Shohin.Application.Interfaces;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

public class DocumentosFunctions
{
    private readonly DigitalizacionService _digitalizacionService;
    private readonly ValidacionService _validacionService;
    private readonly IBlobStorageService _blobService;

    public DocumentosFunctions(
        DigitalizacionService digitalizacionService,
        ValidacionService validacionService,
        IBlobStorageService blobService)
    {
        _digitalizacionService = digitalizacionService;
        _validacionService = validacionService;
        _blobService = blobService;
    }

    [Function("Documentos_ObtenerPorTicket")]
    public async Task<IActionResult> ObtenerPorTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documentos/ticket/{idTicket:int}")] HttpRequest req,
        int idTicket)
    {
        if (int.TryParse(req.Query["pagina"], out int pagina))
        {
            int tamanoPagina = int.TryParse(req.Query["tamanoPagina"], out int tp) ? tp : 10;
            string? filtro = req.Query["filtro"];
            var paginado = await _validacionService.ObtenerDocumentosPorTicketPaginadoAsync(idTicket, pagina, tamanoPagina, filtro);
            return new OkObjectResult(paginado);
        }

        var result = await _validacionService.ObtenerDocumentosPorTicketAsync(idTicket);
        return new OkObjectResult(result);
    }

    [Function("Documentos_ObtenerPorId")]
    public async Task<IActionResult> ObtenerPorId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documentos/{idDocumento:int}")] HttpRequest req,
        int idDocumento)
    {
        var result = await _validacionService.ObtenerDocumentoPorIdAsync(idDocumento);
        if (!result.Exito)
            return new NotFoundObjectResult(result);

        return new OkObjectResult(result);
    }

    [Function("Documentos_DescargarArchivoPorId")]
    public async Task<IActionResult> DescargarArchivoPorId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", Route = "documentos/{idDocumento:int}/archivo")] HttpRequest req,
        int idDocumento)
    {
        var docResult = await _validacionService.ObtenerDocumentoPorIdAsync(idDocumento);
        if (!docResult.Exito || docResult.Datos == null || string.IsNullOrWhiteSpace(docResult.Datos.RutaBlobStorage))
            return new NotFoundObjectResult(ApiResponse<string>.Fail("Documento o ruta de almacenamiento no encontrada."));

        var stream = await _blobService.DescargarArchivoAsync(docResult.Datos.RutaBlobStorage);
        if (stream == null)
            return new NotFoundObjectResult(ApiResponse<string>.Fail("El archivo no se encuentra disponible en Azure Blob Storage."));

        req.HttpContext.Response.Headers.Append("Content-Disposition", $"inline; filename=\"{docResult.Datos.NombreArchivo ?? "documento.pdf"}\"");
        return new FileStreamResult(stream, "application/pdf");
    }

    [Function("Documentos_SubirYProcesar")]
    public async Task<IActionResult> SubirYProcesar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documentos/subir/{idTicket:int}")] HttpRequest req,
        int idTicket)
    {
        if (!req.HasFormContentType)
            return new BadRequestObjectResult(ApiResponse<string>.Fail("Debe enviar una solicitud multipart/form-data."));

        var form = await req.ReadFormAsync();
        var archivo = form.Files["archivo"] ?? (form.Files.Count > 0 ? form.Files[0] : null);

        if (archivo == null || archivo.Length == 0)
            return new BadRequestObjectResult(ApiResponse<string>.Fail("Debe adjuntar un archivo PDF válido."));

        using var stream = archivo.OpenReadStream();
        var result = await _digitalizacionService.ProcesarDocumentoIndividualAsync(idTicket, stream, archivo.FileName);

        if (!result.Exito)
            return new BadRequestObjectResult(result);

        return new OkObjectResult(result);
    }

    [Function("Documentos_CorregirCampo")]
    public async Task<IActionResult> CorregirCampo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "documentos/campo/corregir")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<CorregirCampoDto>();
        if (request == null)
            return new BadRequestObjectResult("Cuerpo de solicitud inválido.");

        var result = await _validacionService.CorregirCampoOcrAsync(request);
        if (!result.Exito)
            return new BadRequestObjectResult(result);

        return new OkObjectResult(result);
    }

    [Function("Documentos_Validar")]
    public async Task<IActionResult> ValidarDocumento(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "documentos/{idDocumento:int}/validar")] HttpRequest req,
        int idDocumento)
    {
        var request = await req.ReadFromJsonAsync<ValidarDocumentoDto>();
        if (request == null)
            return new BadRequestObjectResult("Cuerpo de solicitud inválido.");

        var result = await _validacionService.ValidarDocumentoAsync(idDocumento, request);
        if (!result.Exito)
            return new BadRequestObjectResult(result);

        return new OkObjectResult(result);
    }

    [Function("Documentos_DescargarArchivo")]
    public async Task<IActionResult> DescargarArchivo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", Route = "documentos/archivo/{**ruta}")] HttpRequest req,
        string ruta)
    {
        var stream = await _blobService.DescargarArchivoAsync(ruta);
        if (stream == null)
            return new NotFoundObjectResult(ApiResponse<string>.Fail("Archivo no encontrado en el almacenamiento."));

        return new FileStreamResult(stream, "application/pdf");
    }
}
