namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

/// <summary>
/// Base type that manages domain event collection lifecycles.
/// </summary>
public abstract class HasDomainEventsBase : IHasDomainEvents
{
    // Nullable + lazily initialized, NOT a field initializer: an event-sourced aggregate rebuilt through
    // Marten's live aggregation (FetchForWriting) is allocated without running any constructor, so field
    // initializers never execute on that path. See EventSourcedAggregate's matching comment.
    private List<IDomainEvent>? _domainEvents;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => (_domainEvents ??= []).AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        (_domainEvents ??= []).Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents?.Clear();

    public IEnumerable<IDomainEvent> TakeDomainEvents()
    {
        var events = _domainEvents?.ToArray() ?? [];
        ClearDomainEvents();
        return events;
    }
}


