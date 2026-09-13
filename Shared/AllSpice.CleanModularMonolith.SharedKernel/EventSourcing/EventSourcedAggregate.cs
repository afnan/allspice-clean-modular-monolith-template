using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// Base type for an aggregate whose state is derived from an event stream instead of a table row
/// (ADR-0009). Opt-in per aggregate — most aggregates should stay on <see cref="Entity"/> + EF Core.
/// <para>
/// Shape: command methods validate invariants and call <see cref="Raise"/>; <see cref="Raise"/> applies the
/// event through <see cref="When"/>, records it as uncommitted (for the store) and registers it as a domain
/// event (for same-module <c>IDomainEventHandler&lt;T&gt;</c>s). State transitions live in
/// <c>public void Apply(TEvent)</c> methods that contain no validation — the event store replays history
/// through those same methods by naming convention, so this class never references the store.
/// </para>
/// <para>
/// <b>Do not name the dispatcher <c>Apply</c>.</b> The store's convention scanner also binds interface-typed
/// <c>Apply</c> overloads; an <c>Apply(IDomainEvent)</c> would make it apply every event twice.
/// </para>
/// </summary>
public abstract class EventSourcedAggregate : Entity<Guid>, IAggregateRoot, IEventSourcedAggregate
{
    // Nullable + lazily initialized, NOT a field initializer: Marten's live aggregation (FetchForWriting /
    // LiveStreamAggregation) rebuilds an aggregate by allocating it without running any constructor — field
    // initializers never execute on that path — then replays history straight through Apply(TEvent). A plain
    // `= []` field initializer is therefore only honoured when the aggregate is built through ordinary `new`
    // (ProbeCounter.Start's static factory), and stays null on every aggregate loaded via LoadAsync.
    private List<IDomainEvent>? _uncommittedEvents;

    public long Version { get; private set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => (_uncommittedEvents ??= []).AsReadOnly();

    public bool IsMarkedForArchive { get; private set; }

    /// <summary>Applies the event to in-memory state, then records it for persistence and dispatch.</summary>
    protected void Raise(IDomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        When(@event);
        (_uncommittedEvents ??= []).Add(@event);
        RegisterDomainEvent(@event);
    }

    /// <summary>Routes an event to its <c>Apply(TEvent)</c> method. Pure state transition; no validation.</summary>
    protected abstract void When(IDomainEvent @event);

    /// <summary>
    /// Requests that the stream be archived when the aggregate is saved (e.g. on a terminal "closed" event).
    /// Archived streams are excluded from default queries and projections but never deleted.
    /// </summary>
    protected void MarkForArchive() => IsMarkedForArchive = true;

    void IEventSourcedAggregate.SetVersion(long version) => Version = version;

    void IEventSourcedAggregate.ClearUncommittedEvents() => _uncommittedEvents?.Clear();
}
