using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Shohin.Application.DTOs.Tickets;
using Shohin.Application.Services;

namespace Shohin.Api.Functions;

public class TicketsFunctions
{
    private readonly DigitalizacionService _digitalizacionService;

    public TicketsFunctions(DigitalizacionService digitalizacionService)
    {
        _digitalizacionService = digitalizacionService;
    }

    [Function("Tickets_Obtener")]
    public async Task<IActionResult> ObtenerTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tickets")] HttpRequest req)
    {
        var result = await _digitalizacionService.ObtenerTicketsAsync();
        return new OkObjectResult(result);
    }

    [Function("Tickets_Crear")]
    public async Task<IActionResult> CrearTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tickets")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<CrearTicketDto>();
        if (request == null)
            return new BadRequestObjectResult("Cuerpo de solicitud inválido.");

        var result = await _digitalizacionService.CrearTicketAsync(request);
        return new OkObjectResult(result);
    }
}
