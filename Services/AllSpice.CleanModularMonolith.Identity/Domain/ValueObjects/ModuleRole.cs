using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

namespace AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;

public sealed class ModuleRole : ValueObject
{
    private ModuleRole()
    {
        RoleKey = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    private ModuleRole(string roleKey, string name, string description)
    {
        RoleKey = roleKey;
        Name = name;
        Description = description;
    }

    public string RoleKey { get; }

    public string Name { get; }

    public string Description { get; }

    public static ModuleRole Create(string roleKey, string name, string description)
    {
        Guard.Against.NullOrWhiteSpace(roleKey);
        Guard.Against.NullOrWhiteSpace(name);

        return new ModuleRole(roleKey.Trim(), name.Trim(), description ?? string.Empty);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RoleKey;
    }
}


