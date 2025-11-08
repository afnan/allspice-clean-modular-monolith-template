namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

/// <summary>
/// Base type for domain events providing common metadata.
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    protected DomainEventBase()
    {
        Id = Guid.NewGuid();
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    public DateTimeOffset OccurredOnUtc { get; }

    /// <summary>
    /// Optional correlation identifier to tie the event to a request or transaction.
    /// </summary>
    public string? CorrelationId { get; init; }
}


