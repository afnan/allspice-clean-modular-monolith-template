using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

namespace AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a single module/role pair within a template.
/// </summary>
public sealed class ModuleRoleTemplateRole : ValueObject
{
    private ModuleRoleTemplateRole()
    {
        ModuleKey = string.Empty;
        RoleKey = string.Empty;
    }

    private ModuleRoleTemplateRole(string moduleKey, string roleKey)
    {
        ModuleKey = moduleKey;
        RoleKey = roleKey;
    }

    public string ModuleKey { get; private set; }

    public string RoleKey { get; private set; }

    public static ModuleRoleTemplateRole Create(string moduleKey, string roleKey)
    {
        Guard.Against.NullOrWhiteSpace(moduleKey);
        Guard.Against.NullOrWhiteSpace(roleKey);

        return new ModuleRoleTemplateRole(moduleKey.Trim(), roleKey.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ModuleKey.ToLowerInvariant();
        yield return RoleKey.ToLowerInvariant();
    }
}


