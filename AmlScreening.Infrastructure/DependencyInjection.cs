

using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Services;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Infrastructure.Persistence;
using AmlScreening.Infrastructure.Repositories;
using AmlScreening.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Infrastructure.Provider;

namespace AmlScreening.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration configuration)
        {
            services.AddScoped<IDBConnectionFactory, DBConnectionFactory>();
             services.AddDbContext<AmlScreeningDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("AMLDatabase")));

            services.AddScoped<IUserRepository,UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IAmlRecordRepository, AmlRecordRepository>();

            services.AddScoped<IPasswordService,PasswordService>();
            services.AddScoped<ISubscriptionRepository,SubscriptionRepository>();
            services.AddScoped<IComplianceRuleRepository, ComplianceRuleRepository>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IMenuService, MenuService>();
            services.AddScoped<IAmlWorkflowService, AmlWorkflowService>();
            services.AddScoped<IAmlImportRepository, AmlImportRepository>();
            


            return services;
        }
    }
}
