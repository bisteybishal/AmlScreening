using Microsoft.Extensions.DependencyInjection;
using ScreeningApi.Application.Services;

namespace ScreeningApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CustomerKycService>();
        services.AddSingleton<CustomerKycRiskCalculator>();
        return services;
    }
}

