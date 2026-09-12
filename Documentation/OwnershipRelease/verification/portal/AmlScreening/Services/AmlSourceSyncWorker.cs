using AmlScreening.Application.Interface.Provider;

namespace AmlScreening.Portal.Services;

public sealed class AmlSourceSyncWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<AmlSourceSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["SourceSync:Enabled"], out var enabled) || !enabled) return;
        if (!int.TryParse(configuration["SourceSync:IntervalMinutes"], out var minutes) || minutes < 5)
            throw new InvalidOperationException("SourceSync:IntervalMinutes must be at least 5.");
        var sources = configuration.GetSection("SourceSync:Sources").Get<string[]>() ?? [];
        if (sources.Length == 0) throw new InvalidOperationException("Configure the enabled source providers.");
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var source in sources.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                for (var attempt = 1; attempt <= 3 && !stoppingToken.IsCancellationRequested; attempt++)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var sync = scope.ServiceProvider.GetRequiredService<IAmlSourceSyncService>();
                        var count = await sync.SyncAsync(source, stoppingToken);
                        logger.LogInformation("Source {Source}: imported {Count} records.", source, count);
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Source {Source} sync failed on attempt {Attempt}. Existing data preserved.", source, attempt);
                        if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(10 * attempt), stoppingToken);
                    }
                }
            }
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }
}
