using Microsoft.Extensions.DependencyInjection;
using Shohin.Application.Services;

namespace Shohin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<DigitalizacionService>();
        services.AddScoped<ValidacionService>();
        services.AddScoped<ArchivoHistoricoService>();
        services.AddScoped<ReporteAuditoriaService>();
        services.AddScoped<ParametroService>();

        return services;
    }
}
