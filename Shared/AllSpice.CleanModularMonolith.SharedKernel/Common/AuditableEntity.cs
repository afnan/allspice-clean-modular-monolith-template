using AllSpice.CleanModularMonolith.SharedKernel.Auditing;

namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// An <see cref="Entity"/> that carries creation/modification audit metadata. Audit is a concern independent
/// of aggregate-root-ness: this works for a child entity as well as a root (a root additionally implements
/// <see cref="IAggregateRoot"/>). The columns are stamped centrally by the audit save-interceptor —
/// <see cref="SetCreated"/>/<see cref="SetModified"/> are not part of the domain's public command surface.
/// </summary>
public abstract class AuditableEntity : AuditableEntity<Guid>
{
}

/// <inheritdoc cref="AuditableEntity"/>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
    where TId : IEquatable<TId>
{
    public DateTimeOffset CreatedOnUtc { get; private set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? LastModifiedOnUtc { get; private set; }

    public string? LastModifiedBy { get; private set; }

    public void SetCreated(string? userId)
    {
        CreatedOnUtc = DateTimeOffset.UtcNow;
        CreatedBy = userId;
    }

    public void SetModified(string? userId)
    {
        LastModifiedOnUtc = DateTimeOffset.UtcNow;
        LastModifiedBy = userId;
    }
}
