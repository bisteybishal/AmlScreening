using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Infrastructure.Persistence;
using ScreeningApi.Infrastructure.Repositories;

namespace ScreeningApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddScoped<IDBConnectionFactory,DBConnectionFactory>();
        services.AddScoped<IApiActorRepository,ApiActorRepository>();
        services.AddScoped<ICustomerKycRepository,CustomerKycRepository>();
        return services;
    }
}

