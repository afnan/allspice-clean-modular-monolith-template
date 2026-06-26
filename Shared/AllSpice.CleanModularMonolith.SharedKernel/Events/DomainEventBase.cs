namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

/// <summary>
/// Base type for domain events providing common metadata.
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    /// <summary>
    /// Creates the event stamped with an explicit occurrence time. Prefer this overload and pass a time
    /// sourced from the injected <see cref="TimeProvider"/> (e.g. the aggregate method received a
    /// <c>nowUtc</c> parameter) so event timing is deterministic and testable.
    /// </summary>
    protected DomainEventBase(DateTimeOffset occurredOnUtc)
    {
        Id = Guid.NewGuid();
        OccurredOnUtc = occurredOnUtc;
    }

    /// <summary>
    /// Convenience overload that stamps <see cref="OccurredOnUtc"/> from the system clock. Use only where no
    /// clock is reachable; the explicit-timestamp overload is preferred for testability.
    /// </summary>
    protected DomainEventBase()
        : this(TimeProvider.System.GetUtcNow())
    {
    }

    public Guid Id { get; }

    public DateTimeOffset OccurredOnUtc { get; }

    /// <summary>
    /// Optional correlation identifier to tie the event to a request or transaction.
    /// </summary>
    public string? CorrelationId { get; init; }
}


