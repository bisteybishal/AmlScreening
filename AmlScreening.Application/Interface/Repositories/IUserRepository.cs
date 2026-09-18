using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Repositories;

public interface IUserRepository
{
    //partner

    Task<IReadOnlyList<PartnerOption>>
       GetActivePartnersAsync();

    Task<bool> PartnerCodeExistsAsync(
        string partnerCode);

    Task<int> CreatePartnerAsync(
        Partner partner);
    //Branch

    Task<IReadOnlyList<BranchOption>>
        GetActiveBranchesAsync(
            int partnerId);

    Task<bool> BranchNameExistsAsync(
        int partnerId,
        string branchName);

    Task<int> CreateBranchAsync(
        PartnerBranch branch);


    //user
    Task<UserPageResult> GetPageAsync(
        int? partnerId,
        int? branchId,
        int pageNumber,
        int pageSize);

    Task<IReadOnlyList<RoleOption>>
        GetActiveRolesAsync();

    Task<bool> UsernameExistsAsync(
        string username);

    Task<long> CreateAsync(
        User user,
        IReadOnlyCollection<int> roleIds);
}