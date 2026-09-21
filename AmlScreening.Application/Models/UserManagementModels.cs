using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models
{
    public sealed class UserPermissionOption
    {
        public int PermissionId { get; set; }
        public string PermissionName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
    public sealed class UserManagementOptions
    {
        public IReadOnlyList<RoleOption> Roles { get; init; }
            = Array.Empty<RoleOption>();

        public IReadOnlyList<BranchOption> Branches { get; init; }
            = Array.Empty<BranchOption>();

        public IReadOnlyList<UserPermissionOption> Permissions { get; init; }
            = Array.Empty<UserPermissionOption>();
    }
    public sealed class UserAccessData
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public long AccessVersion { get; set; }

        public List<int> RoleIds { get; set; } = [];
        public List<int> PermissionIds { get; set; } = [];

        public UserManagementOptions Options { get; set; } = new();
    }
    public sealed class UserAuthorizationSnapshot
    {
        public string Username { get; init; } = string.Empty;

        public IReadOnlyList<string> Roles { get; init; }
            = Array.Empty<string>();

        public IReadOnlyList<string> Permissions { get; init; }
            = Array.Empty<string>();
    }

    public sealed record CreateUserCommand(
        int BranchId,
        string Username,
        string PasswordHash,
        string? Email,
        string? Address,
        string? PhoneNumber,
        bool IsActive,
        IReadOnlyCollection<int> RoleIds);

    public sealed record UpdateUserAccessCommand(
        long UserId,
        long AccessVersion,
        bool IsActive,
        IReadOnlyCollection<int> RoleIds,
        IReadOnlyCollection<int> PermissionIds);

    public sealed class UserAccessInfo
    {
        public long UserId { get; set; }


        public int PartnerId { get; set; }


        public string PartnerName { get; set; }
            = string.Empty;


        public int BranchId { get; set; }


        public string BranchName { get; set; }
            = string.Empty;


        public string Username { get; set; }
            = string.Empty;


        public bool IsActive { get; set; }
    }
}
