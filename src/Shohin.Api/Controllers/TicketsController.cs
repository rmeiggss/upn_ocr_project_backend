using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shohin.Application.DTOs.Tickets;
using Shohin.Application.Services;

namespace Shohin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly DigitalizacionService _digitalizacionService;

    public TicketsController(DigitalizacionService digitalizacionService)
    {
        _digitalizacionService = digitalizacionService;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTickets()
    {
        var result = await _digitalizacionService.ObtenerTicketsAsync();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CrearTicket([FromBody] CrearTicketDto request)
    {
        var result = await _digitalizacionService.CrearTicketAsync(request);
        return Ok(result);
    }
}
