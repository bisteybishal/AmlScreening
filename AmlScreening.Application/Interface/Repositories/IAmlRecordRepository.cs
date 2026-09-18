using AmlScreening.Domain.Entities;
using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IAmlRecordRepository
{
    Task<IReadOnlyList<AmlRecord>> FindMatchNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<AmlRecord>> GetPageAsync(
        string? search,
        string? recordType,
        bool? isActive,
        int pageNumber,
        int pageSize,CancellationToken cancellationToken=default);
    Task<AmlRecordSummary> GetSummaryAsync(CancellationToken cancellationToken=default);
    Task<IReadOnlyList<AmlRecord>> GetRecentAsync(int maximumRecords,CancellationToken cancellationToken=default);
    Task<bool> ReferenceExistsAsync(string referenceNo, string sourceName, int? excludedId = null, CancellationToken cancellationToken = default);
    Task<AmlRecord?> GetByIdAsync(int id,CancellationToken cancellationToken=default);
    Task<int> CreateAsync(AmlRecord record, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(AmlRecord record, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<AmlRecord?> GetPhotoAsync(int id, CancellationToken cancellationToken = default);

    
}
