using AllSpice.CleanModularMonolith.SharedKernel.Auditing;

namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// An <see cref="Entity"/> that carries creation/modification audit metadata. Audit is a concern independent
/// of aggregate-root-ness: this works for a child entity as well as a root (a root additionally implements
/// <see cref="IAggregateRoot"/>). The columns are read-only on the domain — stamped centrally by
/// <c>AuditableEntityInterceptor</c> on save, never set by domain code.
/// </summary>
public abstract class AuditableEntity : AuditableEntity<Guid>
{
}

/// <inheritdoc cref="AuditableEntity"/>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
    where TId : IEquatable<TId>
{
    // Private setters: EF Core's change tracker writes these (the interceptor sets them via PropertyEntry on
    // save). No construction-time default for CreatedOnUtc — it is meaningless until persisted, and the
    // interceptor stamps it for every inserted row.
    public DateTimeOffset CreatedOnUtc { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? LastModifiedOnUtc { get; private set; }

    public string? LastModifiedBy { get; private set; }
}
