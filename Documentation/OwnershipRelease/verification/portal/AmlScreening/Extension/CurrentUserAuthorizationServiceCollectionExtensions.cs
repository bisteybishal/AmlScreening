using AmlScreening.Application.Interface;
using AmlScreening.Portal.Security;
using AmlScreening.Portal.Services;
using Microsoft.AspNetCore.Authorization;

namespace AmlScreening.Portal.Extension;

public static class CurrentUserAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddCurrentUserAuthorization(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IAuthorizationHandler, AmlRecordAuthorizationHandler>();

        return services;
    }
}