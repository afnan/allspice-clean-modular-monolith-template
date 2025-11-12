using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>
/// Authorization handler that validates module-role assignments on a principal.
/// </summary>
public sealed class ModuleRoleAuthorizationHandler : AuthorizationHandler<ModuleRoleRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ModuleRoleRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.HasModuleRole(requirement.ModuleKey, requirement.RoleKey))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}


