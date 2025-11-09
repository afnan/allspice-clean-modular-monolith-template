using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;

/// <summary>
/// Represents the assignment of a module-specific role to an Authentik user, including audit metadata.
/// </summary>
public sealed class ModuleRoleAssignment : AuditableAggregateRoot
{
    private ModuleRoleAssignment()
    {
        ModuleKey = string.Empty;
        RoleKey = string.Empty;
        AssignedBy = string.Empty;
    }

    private ModuleRoleAssignment(ExternalUserId userId, string moduleKey, string roleKey, string assignedBy)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ModuleKey = moduleKey;
        RoleKey = roleKey;
        AssignedBy = assignedBy;
        AssignedUtc = DateTimeOffset.UtcNow;

        SetCreated(assignedBy);
    }

    /// <summary>
    /// Gets the Authentik user receiving the module role assignment.
    /// </summary>
    public ExternalUserId UserId { get; private set; } = ExternalUserId.From(Guid.Empty.ToString());

    /// <summary>
    /// Gets the module identifier (e.g. <c>hr</c>).
    /// </summary>
    public string ModuleKey { get; private set; }

    /// <summary>
    /// Gets the role key within the module (e.g. <c>admin</c>).
    /// </summary>
    public string RoleKey { get; private set; }

    /// <summary>
    /// Gets the identifier of the actor who granted the role.
    /// </summary>
    public string AssignedBy { get; private set; }

    /// <summary>
    /// Gets the moment the role was granted.
    /// </summary>
    public DateTimeOffset AssignedUtc { get; private set; }

    /// <summary>
    /// Gets the moment the role was revoked, if applicable.
    /// </summary>
    public DateTimeOffset? RevokedUtc { get; private set; }

    /// <summary>
    /// Creates a new module role assignment for a user.
    /// </summary>
    public static ModuleRoleAssignment Create(ExternalUserId userId, string moduleKey, string roleKey, string assignedBy)
    {
        Guard.Against.Null(userId);
        Guard.Against.NullOrWhiteSpace(moduleKey);
        Guard.Against.NullOrWhiteSpace(roleKey);
        Guard.Against.NullOrWhiteSpace(assignedBy);

        return new ModuleRoleAssignment(userId, moduleKey.Trim(), roleKey.Trim(), assignedBy.Trim());
    }

    /// <summary>
    /// Marks the role as revoked by the supplied actor.
    /// </summary>
    public void Revoke(string revokedBy)
    {
        Guard.Against.NullOrWhiteSpace(revokedBy);

        RevokedUtc = DateTimeOffset.UtcNow;
        SetModified(revokedBy);
    }

    /// <summary>
    /// Indicates whether the assignment is currently active.
    /// </summary>
    public bool IsActive() => RevokedUtc is null;
}


