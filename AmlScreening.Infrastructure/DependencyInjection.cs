using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Security;
using AmlScreening.Infrastructure.Persistence;
using AmlScreening.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AmlScreening.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // Register your infrastructure services here
            services.AddScoped<IDBConnectionFactory, DBConnectionFactory>();
            services.AddScoped<IUserRepository,UserRepository>();
            services.AddScoped<IAmlRecordRepository, AmlRecordRepository>();

            services.AddScoped<IPasswordService,PasswordService>();


            return services;
        }
    }
}
