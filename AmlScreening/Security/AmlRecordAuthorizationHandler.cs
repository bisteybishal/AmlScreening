using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace AmlScreening.Portal.Security;

public sealed class AmlRecordAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, AmlRecord>
{
    private readonly ICurrentUserContext _currentUser;

    public AmlRecordAuthorizationHandler(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        AmlRecord resource)
    {
        if (!_currentUser.IsAuthenticated)
            return Task.CompletedTask;

        if (_currentUser.HasPermission(AppPermissions.AmlRecordsViewAll))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (resource.ClientId == _currentUser.ClientId &&
            (_currentUser.HasPermission(AppPermissions.AmlRecordsViewClient) ||
             resource.OwnerUserId == _currentUser.UserId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
