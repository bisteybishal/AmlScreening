using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Repositories;

public interface IAmlRecordRepository
{
    Task<PagedResult<AmlRecord>> GetAllAsync(int pagenmber,int pagesize);
    Task<IReadOnlyList<AmlRecord>> GetAllAsync();
    Task<AmlRecord?> GetByIdAsync(int id);
    Task<int> CreateAsync(AmlRecord record);
    Task<bool> UpdateAsync(AmlRecord record,string updateby);
    Task<bool> DeleteAsync(int id, string deletedBy);
}
