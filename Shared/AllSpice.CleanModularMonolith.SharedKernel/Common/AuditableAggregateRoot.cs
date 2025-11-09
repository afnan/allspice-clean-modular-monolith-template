using AllSpice.CleanModularMonolith.SharedKernel.Auditing;

namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// Aggregate root base that tracks creation and modification metadata.
/// </summary>
public abstract class AuditableAggregateRoot : AggregateRoot, IAuditable
{
    public DateTimeOffset CreatedOnUtc { get; private set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? LastModifiedOnUtc { get; private set; }

    public string? LastModifiedBy { get; private set; }

    public void SetCreated(string? userId)
    {
        CreatedOnUtc = DateTimeOffset.UtcNow;
        CreatedBy = userId;
        LastModifiedOnUtc = CreatedOnUtc;
        LastModifiedBy = userId;
    }

    public void SetModified(string? userId)
    {
        LastModifiedOnUtc = DateTimeOffset.UtcNow;
        LastModifiedBy = userId;
    }
}

