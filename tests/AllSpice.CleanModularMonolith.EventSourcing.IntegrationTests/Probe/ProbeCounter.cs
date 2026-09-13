using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;

public sealed record CounterStarted(Guid CounterId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record CounterIncremented(Guid CounterId, int By, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record CounterRetired(Guid CounterId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

/// <summary>Minimal event-sourced aggregate used to prove the store mechanics independent of any module.</summary>
public sealed class ProbeCounter : EventSourcedAggregate
{
    private ProbeCounter()
    {
    }

    public int Value { get; private set; }
    public bool Retired { get; private set; }

    public static ProbeCounter Start(Guid id, DateTimeOffset nowUtc)
    {
        var counter = new ProbeCounter();
        counter.Raise(new CounterStarted(id, nowUtc));
        return counter;
    }

    public void Increment(int by, DateTimeOffset nowUtc) => Raise(new CounterIncremented(Id, by, nowUtc));

    public void Retire(DateTimeOffset nowUtc)
    {
        Raise(new CounterRetired(Id, nowUtc));
        MarkForArchive();
    }

    public void Apply(CounterStarted e) => Id = e.CounterId;
    public void Apply(CounterIncremented e) => Value += e.By;
    public void Apply(CounterRetired e) => Retired = true;

    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case CounterStarted e: Apply(e); break;
            case CounterIncremented e: Apply(e); break;
            case CounterRetired e: Apply(e); break;
            default: throw new InvalidOperationException($"Unhandled event {@event.GetType().Name}");
        }
    }
}
