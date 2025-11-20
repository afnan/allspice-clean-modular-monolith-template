using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;

/// <summary>
/// A reusable bundle of module roles that can be applied to a user.
/// </summary>
public sealed class ModuleRoleTemplate : AggregateRoot
{
    private readonly List<ModuleRoleTemplateRole> _roles = new();

    private ModuleRoleTemplate()
    {
        TemplateKey = string.Empty;
        Name = string.Empty;
    }

    private ModuleRoleTemplate(string templateKey, string name, string? description)
    {
        Id = Guid.NewGuid();
        TemplateKey = templateKey;
        Name = name;
        Description = description ?? string.Empty;
    }

    public string TemplateKey { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public IReadOnlyCollection<ModuleRoleTemplateRole> Roles => _roles.AsReadOnly();

    public static ModuleRoleTemplate Create(string templateKey, string name, string? description, IEnumerable<ModuleRoleTemplateRole>? roles = null)
    {
        Guard.Against.NullOrWhiteSpace(templateKey);
        Guard.Against.NullOrWhiteSpace(name);

        var template = new ModuleRoleTemplate(templateKey.Trim().ToLowerInvariant(), name.Trim(), description?.Trim());

        if (roles is not null)
        {
            template.ReplaceRoles(roles);
        }

        return template;
    }

    public void UpdateDetails(string name, string? description)
    {
        Guard.Against.NullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    public void ReplaceRoles(IEnumerable<ModuleRoleTemplateRole> roles)
    {
        Guard.Against.Null(roles);

        _roles.Clear();

        foreach (var role in roles)
        {
            AddRole(role.ModuleKey, role.RoleKey);
        }
    }

    public void AddRole(string moduleKey, string roleKey)
    {
        var candidate = ModuleRoleTemplateRole.Create(moduleKey, roleKey);

        if (_roles.Any(existing => existing.ModuleKey.Equals(candidate.ModuleKey, StringComparison.OrdinalIgnoreCase)
                                   && existing.RoleKey.Equals(candidate.RoleKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _roles.Add(candidate);
    }

    public void RemoveRole(string moduleKey, string roleKey)
    {
        _roles.RemoveAll(role =>
            role.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase) &&
            role.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase));
    }
}


