using AmlScreening.Application.Interface;
using AmlScreening.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AmlScreening.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }
}
