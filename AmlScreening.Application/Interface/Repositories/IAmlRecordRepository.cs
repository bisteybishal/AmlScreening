using AmlScreening.Domain.Entities;
using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IAmlRecordRepository
{
    Task<PagedResult<AmlRecord>> GetPageAsync(
        string? search,
        string? recordType,
        bool? isActive,
        int pageNumber,
        int pageSize);
    Task<AmlRecordSummary> GetSummaryAsync();
    Task<IReadOnlyList<AmlRecord>> GetRecentAsync(int maximumRecords);
    Task<bool> ReferenceExistsAsync(string referenceNo, string sourceName, int? excludedId = null);
   // Task<IReadOnlyList<AmlRecord>> GetAllAsync();
    Task<AmlRecord?> GetByIdAsync(int id);
    Task<int> CreateAsync(AmlRecord record);
    Task<bool> UpdateAsync(AmlRecord record,string updateby);
    Task<bool> DeleteAsync(int id, string deletedBy);
}
