using AmlScreening.Application.Interface.Provider;

namespace AmlScreening.Application.Services;
public sealed class AmlSourceSyncService(IEnumerable<IAmlSourceStrategy> strategies, IAmlImportRepository repository) : IAmlSourceSyncService
{
    public async Task<int> SyncAsync(string sourceCode, CancellationToken cancellationToken = default)
    {
        var code = sourceCode?.Trim().ToUpperInvariant();
        var strategy = strategies.SingleOrDefault(x => x.SourceCode.Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("This source has no configured provider.");
        var records = await strategy.GetRecordsAsync(cancellationToken);
        // An empty feed may be a provider error: preserve the last good dataset.
        if (records.Count == 0) throw new InvalidOperationException("Source returned no usable records. Existing records were preserved.");
        if (records.Any(x => string.IsNullOrWhiteSpace(x.FullName) || string.IsNullOrWhiteSpace(x.ReferenceNo)))
            throw new InvalidOperationException("Source contains invalid records. Existing records were preserved.");
        if (records.GroupBy(x => new { x.SourceName, x.ReferenceNo }).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Source contains duplicate references. Review the feed before importing.");
        await repository.SaveAsync(records, cancellationToken);
        return records.Count;
    }
}
