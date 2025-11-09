using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public sealed class ModuleRoleAuthorizationHandler : AuthorizationHandler<ModuleRoleRequirement>
{
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


