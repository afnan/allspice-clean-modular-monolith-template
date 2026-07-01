using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>An app-side role whose <see cref="Key"/> mirrors a Keycloak realm-role name.</summary>
public sealed class Role : AuditableEntity, IAggregateRoot
{
    private Role() { }

    private Role(string key, string? description)
    {
        Id = Guid.NewGuid();
        Key = key;
        Description = description;
    }

    public string Key { get; private set; } = default!;
    public string? Description { get; private set; }

    public static Role Create(string key, string? description)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Role key is required.", nameof(key));
        }

        return new Role(key.Trim().ToLowerInvariant(), description);
    }
}
