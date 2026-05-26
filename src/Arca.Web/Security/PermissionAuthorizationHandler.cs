using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Arca.Web.Security;

public sealed class PermissionAuthorizationHandler(
    ICurrentUserService currentUser,
    IPermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return;
        }

        if (currentUser.IsSuperAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var hasPermission = await permissionService.HasPermissionAsync(
            currentUser.UserId.Value,
            requirement.Permission,
            currentUser.CurrentTenantId,
            currentUser.CurrentStoreId);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
