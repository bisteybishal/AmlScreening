using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Portal.Security;

public sealed class AmlRecordAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, AmlRecord>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly AmlScreeningDbContext _db;

    public AmlRecordAuthorizationHandler(
        ICurrentUserContext currentUser, AmlScreeningDbContext db)
    {
        _currentUser = currentUser;
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        AmlRecord resource)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId <= 0 ||
            _currentUser.ClientId <= 0)
            return;

        var permission = requirement.Name switch
        {
            nameof(AmlRecordOperations.Read) => AppPermissions.AmlRecordsView,
            nameof(AmlRecordOperations.Update) => AppPermissions.AmlRecordsEdit,
            nameof(AmlRecordOperations.Delete) => AppPermissions.AmlRecordsDelete,
            _ => null
        };
        if (permission is null || !_currentUser.HasPermission(permission))
            return;

        // Read the real row through the global visibility rule; do not trust form ownership.
        var stored = await _db.AmlRecords.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == resource.Id);
        if (stored is null) return;

        var isSystemAdministrator = _currentUser.IsAdministrator &&
            _currentUser.HasPermission(AppPermissions.AmlRecordsViewAll);
        var isOwner = !stored.IsShared &&
            stored.ClientId == _currentUser.ClientId &&
            stored.OwnerUserId == _currentUser.UserId;

        if (requirement.Name == AmlRecordOperations.Read.Name ||
            isSystemAdministrator || isOwner)
            context.Succeed(requirement);
    }
}
