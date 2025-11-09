using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public sealed class ModuleRoleRequirement : IAuthorizationRequirement
{
    public ModuleRoleRequirement(string moduleKey, string roleKey)
    {
        ModuleKey = moduleKey ?? throw new ArgumentNullException(nameof(moduleKey));
        RoleKey = roleKey ?? throw new ArgumentNullException(nameof(roleKey));
    }

    public string ModuleKey { get; }

    public string RoleKey { get; }
}


