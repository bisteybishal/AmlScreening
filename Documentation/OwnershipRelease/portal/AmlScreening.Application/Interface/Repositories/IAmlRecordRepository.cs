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
        int pageSize);
    Task<AmlRecordSummary> GetSummaryAsync(CancellationToken cancellationToken=default);
    Task<IReadOnlyList<AmlRecord>> GetRecentAsync(int maximumRecords);
    Task<bool> ReferenceExistsAsync(string referenceNo, string sourceName, int? excludedId = null);
    Task<AmlRecord?> GetByIdAsync(int id);
    Task<int> CreateAsync(AmlRecord record);
    Task<bool> UpdateAsync(AmlRecord record, string updateby);
    Task<bool> DeleteAsync(int id, string deletedBy);

    Task<AmlRecord?> GetPhotoAsync(
    int id,
    CancellationToken cancellationToken = default);
}
