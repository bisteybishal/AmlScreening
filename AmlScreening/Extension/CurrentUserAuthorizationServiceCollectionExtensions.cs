using AmlScreening.Application.Interface;
using AmlScreening.Portal.Services;

namespace AmlScreening.Portal.Extension;

public static class CurrentUserAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddCurrentUserAuthorization(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        return services;
    }
}