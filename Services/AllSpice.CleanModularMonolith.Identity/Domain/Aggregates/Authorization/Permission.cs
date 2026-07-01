using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>A grantable permission. <see cref="IsSystem"/> keys are code-referenced and deletion-protected.</summary>
public sealed class Permission : AuditableEntity, IAggregateRoot
{
    private Permission() { }

    private Permission(string key, string? description, bool isSystem)
    {
        Id = Guid.NewGuid();
        Key = key;
        Description = description ?? string.Empty;
        IsSystem = isSystem;
    }

    public string Key { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsSystem { get; private set; }

    public static Permission Create(string key, string? description, bool isSystem)
    {
        if (!PermissionKey.IsValid(key))
        {
            throw new ArgumentException($"Invalid permission key '{key}'.", nameof(key));
        }

        return new Permission(key, description, isSystem);
    }
}
