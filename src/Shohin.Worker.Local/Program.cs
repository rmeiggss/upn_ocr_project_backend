using Shohin.Application;
using Shohin.Application.Interfaces;
using Shohin.Infrastructure;
using Shohin.Infrastructure.Persistence;
using Shohin.Worker.Local;

var builder = Host.CreateApplicationBuilder(args);

// Registrar capas de la Clean Architecture
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Registrar Worker Service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Sembrar catálogo de parámetros iniciales en la base de datos
try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
    await ApplicationDbContextSeed.SeedAsync(dbContext, jwtService);
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Worker>>();
    logger.LogWarning("⚠️ No se pudo ejecutar el Seed inicial en la base de datos: {Message}. El agente continuará y reintentará la conexión en segundo plano.", ex.Message);
}

host.Run();
