namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();

    IEnumerable<IDomainEvent> TakeDomainEvents();
}


