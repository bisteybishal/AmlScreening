using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AmlScreening.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAmlSourceSyncService, AmlSourceSyncService>();
        
        
        return services;
    }
}
