using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// Store-facing surface of an event-sourced aggregate. Repositories and the transaction participant use it
/// to read uncommitted events, stamp the stream version after a load, and clear events after a flush.
/// Application code should depend on the concrete aggregate, not on this interface.
/// </summary>
public interface IEventSourcedAggregate : IHasDomainEvents
{
    Guid Id { get; }

    /// <summary>Stream version at load time (0 for a new aggregate). Set by the repository, not by the aggregate.</summary>
    long Version { get; }

    /// <summary>Events raised since the last flush, in order. These are what the store appends.</summary>
    IReadOnlyList<IDomainEvent> UncommittedEvents { get; }

    /// <summary>True when the aggregate asked for its stream to be archived at the next save.</summary>
    bool IsMarkedForArchive { get; }

    void SetVersion(long version);

    void ClearUncommittedEvents();
}
