using Microsoft.AspNetCore.DataProtection;

namespace AmlScreening.Portal.Extension;

public static class DataProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddCustomDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "AmlScreening.Portal");

        var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

            if (OperatingSystem.IsWindows() && !environment.IsDevelopment())
                dataProtectionBuilder.ProtectKeysWithDpapi();
        }

        return services;
    }
}