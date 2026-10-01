using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Shohin.Api.Middlewares;
using Shohin.Application;
using Shohin.Application.Interfaces;
using Shohin.Infrastructure;
using Shohin.Infrastructure.Persistence;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// 1. Inyección de Capas de la Clean Architecture
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Controladores
builder.Services.AddControllers();

// 3. Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. Configuración de Autenticación JWT
var secretKey = builder.Configuration["Jwt:SecretKey"] ?? "ShohinEnterpriseSuperSecretKey2026_ArquitecturaUPN_Ciclo8_MasterKey";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "ShohinApi";
var audience = builder.Configuration["Jwt:Audience"] ?? "ShohinApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 5. Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Shohin S.A. - API de Digitalización y Archivo Histórico",
        Version = "v1",
        Description = "Backend oficial del curso Diseño y Arquitectura de Software (Ciclo 8 - UPN). Arquitectura Limpia basada en BCE."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Autenticación JWT. Ingrese 'Bearer' seguido de su token. Ejemplo: 'Bearer eyJhbGciOi...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
});

var app = builder.Build();

// 6. Migración y Sembrado Automático de Base de Datos en segundo plano (para no bloquear el handshake gRPC de Azure Functions)
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var jwtService = services.GetRequiredService<IJwtService>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        await ApplicationDbContextSeed.SeedAsync(dbContext, jwtService);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Error durante la inicialización de la base de datos en segundo plano.");
    }
});

await app.RunAsync();
