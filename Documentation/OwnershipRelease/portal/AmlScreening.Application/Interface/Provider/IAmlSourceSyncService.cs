namespace AmlScreening.Application.Interface.Provider;
public interface IAmlSourceSyncService
{
    Task<int> SyncAsync(string sourceCode, CancellationToken cancellationToken = default);
}
