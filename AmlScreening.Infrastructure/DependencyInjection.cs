

using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Services;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Infrastructure.Persistence;
using AmlScreening.Infrastructure.Provider;
using AmlScreening.Infrastructure.Repositories;
using AmlScreening.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddScoped<IRiskScoringService, RiskScoringService>();
            services.AddScoped<IAmlImportRepository, AmlImportRepository>();
            services.AddScoped<IErrorRepository, ErrorRepository>();
            services.AddScoped<IComplianceDashboardRepository, ComplianceDashboardRepository>();


            services.AddHttpClient("UnAmlClient", client =>
            {
                client.BaseAddress = new Uri("https://scsanctions.un.org/");
                client.Timeout = TimeSpan.FromSeconds(300);
            });

            services.AddHttpClient("EuAmlClient", client =>
            {
                client.BaseAddress = new Uri("https://webgate.ec.europa.eu/");
                client.Timeout = TimeSpan.FromSeconds(600);
            });

            services.AddScoped<IAmlSourceStrategy, UnAmlSourceStrategy>();
            services.AddScoped<IAmlSourceStrategy, EuAmlSourceStrategy>();

            // Optional: resolves the right strategy by SourceCode at runtime
            services.AddScoped<IAmlSourceStrategyFactory, AmlSourceStrategyFactory>();



            return services;
        }
    }
}
