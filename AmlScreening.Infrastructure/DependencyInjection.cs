

using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Services;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Infrastructure.Persistence;
using AmlScreening.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration configuration)
        {
            // Register your infrastructure services here
            services.AddScoped<IDBConnectionFactory, DBConnectionFactory>();
             services.AddDbContext<AmlScreeningDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("AMLDatabase")));

            services.AddScoped<IUserRepository,UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IAmlRecordRepository, AmlRecordRepository>();

            services.AddScoped<IPasswordService,PasswordService>();
            services.AddScoped<ISubscriptionRepository,SubscriptionRepository>();


            return services;
        }
    }
}
