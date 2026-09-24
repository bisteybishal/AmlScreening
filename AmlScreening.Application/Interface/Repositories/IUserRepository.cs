using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Repositories;

public interface IUserRepository
{
    //partner

    Task<PartnerPageResult> GetPartnersAsync(int pageNumber, int pageSize);
    Task<IReadOnlyList<PartnerOption>> GetActivePartnersAsync();

    Task<Partner?> GetPartnerByIdAsync(int partnerId);

    Task<bool> PartnerCodeExistsAsync(string partnerCode);
    Task<bool> PartnerCodeExistsAsync(string partnercode, int excludepartnerId);

    Task<int> CreatePartnerAsync(Partner partner);

    Task<bool> UpdatePartnerAsync(Partner partner);

    //Branch

    Task<IReadOnlyList<BranchOption>> GetActiveBranchesAsync(int partnerId);

    Task<IReadOnlyList<BranchListItem>> GetBranchesByPartnerAsync(int partnerid);
    Task<PartnerBranch?> GetBranchByIdAsync(int branchId);
    Task<bool> BranchNameExistsAsync(int partnerId,string branchName);
    Task<bool> BranchNameExistsAsync(int partnerId, string branchName,int excludePartnerid);

    Task<int> CreateBranchAsync(PartnerBranch branch);

    Task<bool> UpdateBranchAsync(PartnerBranch branch);
   


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

    // =========================================================
    // USER ACCESS
    // =========================================================

    Task<UserAccessInfo?>
        GetAccessAsync(
            long userId);


    Task<IReadOnlyList<int>>
        GetUserRoleIdsAsync(
            long userId);


    Task UpdateAccessAsync(
        long userId,
        bool isActive,
        IReadOnlyCollection<int> roleIds);

    Task<User?> GetUserByIdAsync(long userId);

    Task<bool> UpdateUserProfileAsync(
        long userId,
        string? address,
        string? email,
        string? phoneNumber,
        bool isActive);
}