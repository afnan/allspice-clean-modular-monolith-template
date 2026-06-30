using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public sealed class PermissionAuthorizationHandler(ICurrentUserPermissions currentUserPermissions)
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserPermissions _currentUserPermissions = currentUserPermissions;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await _currentUserPermissions.HasPermissionAsync(requirement.PermissionKey))
        {
            context.Succeed(requirement);
        }
    }
}
