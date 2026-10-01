using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shohin.Application.DTOs.Common;
using Shohin.Application.DTOs.Documentos;
using Shohin.Application.Interfaces;
using Shohin.Application.Services;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentosController : ControllerBase
{
    private readonly DigitalizacionService _digitalizacionService;
    private readonly ValidacionService _validacionService;
    private readonly IBlobStorageService _blobService;

    public DocumentosController(
        DigitalizacionService digitalizacionService,
        ValidacionService validacionService,
        IBlobStorageService blobService)
    {
        _digitalizacionService = digitalizacionService;
        _validacionService = validacionService;
        _blobService = blobService;
    }

    [HttpGet("ticket/{idTicket}")]
    public async Task<IActionResult> ObtenerPorTicket(int idTicket)
    {
        var result = await _validacionService.ObtenerDocumentosPorTicketAsync(idTicket);
        return Ok(result);
    }

    [HttpGet("{idDocumento}")]
    public async Task<IActionResult> ObtenerPorId(int idDocumento)
    {
        var result = await _validacionService.ObtenerDocumentoPorIdAsync(idDocumento);
        if (!result.Exito)
            return NotFound(result);

        return Ok(result);
    }

    // CUS-01: Carga y procesamiento OCR
    [HttpPost("subir/{idTicket}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirYProcesar(int idTicket, IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Debe adjuntar un archivo PDF válido."));

        using var stream = archivo.OpenReadStream();
        var result = await _digitalizacionService.ProcesarDocumentoIndividualAsync(idTicket, stream, archivo.FileName);

        if (!result.Exito)
            return BadRequest(result);

        return Ok(result);
    }

    // CUS-05: Corregir datos extraídos
    [HttpPut("campo/corregir")]
    public async Task<IActionResult> CorregirCampo([FromBody] CorregirCampoDto request)
    {
        var result = await _validacionService.CorregirCampoOcrAsync(request);
        if (!result.Exito)
            return BadRequest(result);

        return Ok(result);
    }

    // CUS-02: Validar documento procesado
    [HttpPut("{idDocumento}/validar")]
    public async Task<IActionResult> ValidarDocumento(int idDocumento, [FromBody] ValidarDocumentoDto request)
    {
        var result = await _validacionService.ValidarDocumentoAsync(idDocumento, request);
        if (!result.Exito)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("archivo/{**ruta}")]
    public async Task<IActionResult> DescargarArchivo(string ruta)
    {
        var stream = await _blobService.DescargarArchivoAsync(ruta);
        if (stream == null)
            return NotFound(ApiResponse<string>.Fail("Archivo no encontrado en el almacenamiento."));

        return File(stream, "application/pdf");
    }
}
