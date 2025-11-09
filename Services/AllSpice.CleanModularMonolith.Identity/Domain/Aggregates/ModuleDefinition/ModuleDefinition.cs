using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;

public sealed class ModuleDefinition : AggregateRoot
{
    private readonly List<ModuleRole> _roles = new();

    private ModuleDefinition()
    {
        Key = string.Empty;
        DisplayName = string.Empty;
        Description = string.Empty;
    }

    private ModuleDefinition(string key, string displayName, string description)
    {
        Id = Guid.NewGuid();
        Key = key;
        DisplayName = displayName;
        Description = description;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    public string Key { get; private set; }

    public string DisplayName { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public IReadOnlyCollection<ModuleRole> Roles => _roles.AsReadOnly();

    public static ModuleDefinition Create(string key, string displayName, string description)
    {
        Guard.Against.NullOrWhiteSpace(key);
        Guard.Against.NullOrWhiteSpace(displayName);

        return new ModuleDefinition(key.Trim(), displayName.Trim(), description?.Trim() ?? string.Empty);
    }

    public ModuleRole AddRole(string roleKey, string roleName, string description)
    {
        Guard.Against.NullOrWhiteSpace(roleKey);
        Guard.Against.NullOrWhiteSpace(roleName);

        if (_roles.Any(role => role.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Role '{roleKey}' already exists for module '{Key}'.");
        }

        var moduleRole = ModuleRole.Create(roleKey.Trim(), roleName.Trim(), description?.Trim() ?? string.Empty);
        _roles.Add(moduleRole);
        return moduleRole;
    }
}


