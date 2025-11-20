using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>
/// Authorization requirement that enforces a specific module/role combination.
/// </summary>
public sealed class ModuleRoleRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleRoleRequirement"/> class.
    /// </summary>
    /// <param name="moduleKey">Module identifier.</param>
    /// <param name="roleKey">Role within the module.</param>
    public ModuleRoleRequirement(string moduleKey, string roleKey)
    {
        ModuleKey = moduleKey ?? throw new ArgumentNullException(nameof(moduleKey));
        RoleKey = roleKey ?? throw new ArgumentNullException(nameof(roleKey));
    }

    /// <summary>Gets the module identifier.</summary>
    public string ModuleKey { get; }

    /// <summary>Gets the role key within the module.</summary>
    public string RoleKey { get; }
}


