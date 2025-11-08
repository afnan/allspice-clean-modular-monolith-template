using AllSpice.CleanModularMonolith.SharedKernel.Auditing;

namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

public abstract class SoftDeletableEntity : SoftDeletableEntity<Guid>
{
}

public abstract class SoftDeletableEntity<TId> : AuditableEntity<TId>, ISoftDelete
    where TId : IEquatable<TId>
{
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedOnUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public void MarkDeleted(string? userId)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedOnUtc = null;
        DeletedBy = null;
    }
}


