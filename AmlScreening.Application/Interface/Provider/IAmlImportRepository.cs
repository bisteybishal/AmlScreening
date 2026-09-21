using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Provider
{
    public interface IAmlImportRepository
    {
        Task<int> SaveAsync(string sourceCode,IReadOnlyList<AmlRecord> records,CancellationToken cancellation=default);
    }
}
