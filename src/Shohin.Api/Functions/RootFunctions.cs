using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Shohin.Api.Functions;

public class RootFunctions
{
    [Function("HealthCheck")]
    public IActionResult HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            status = "Healthy",
            service = "Shohin S.A. Digitalización API",
            architecture = "Azure Functions .NET 10 (Clean Architecture / BCE)",
            timestamp = DateTime.UtcNow
        });
    }
}
