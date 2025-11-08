namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

/// <summary>
/// Base type that manages domain event collection lifecycles.
/// </summary>
public abstract class HasDomainEventsBase : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public IEnumerable<IDomainEvent> TakeDomainEvents()
    {
        var events = _domainEvents.ToArray();
        ClearDomainEvents();
        return events;
    }
}


