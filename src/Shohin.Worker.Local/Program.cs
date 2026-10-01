using Shohin.Application;
using Shohin.Infrastructure;
using Shohin.Worker.Local;

var builder = Host.CreateApplicationBuilder(args);

// Registrar capas de la Clean Architecture
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Registrar Worker Service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
