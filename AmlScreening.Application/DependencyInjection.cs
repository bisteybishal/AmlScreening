using AmlScreening.Application.Interface.Services;
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
