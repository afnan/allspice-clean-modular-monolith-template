using AllSpice.CleanModularMonolith.SharedKernel.Auditing;

namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// Aggregate root with audit metadata and domain event support.
/// </summary>
public abstract class AuditableEntity : AuditableEntity<Guid>
{
}

public abstract class AuditableEntity<TId> : AggregateRoot<TId>, IAuditable
    where TId : IEquatable<TId>
{
    public DateTimeOffset CreatedOnUtc { get; private set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; private set; }
        = null;

    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
        = null;

    public string? LastModifiedBy { get; private set; }
        = null;

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


