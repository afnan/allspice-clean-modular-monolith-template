using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.EventSourcing;

public class EventSourcedAggregateTests
{
    private sealed record CounterStarted(Guid CounterId) : IDomainEvent;
    private sealed record CounterIncremented(Guid CounterId, int By) : IDomainEvent;

    private sealed class Counter : EventSourcedAggregate
    {
        public int Value { get; private set; }

        public static Counter Start(Guid id)
        {
            var counter = new Counter();
            counter.Raise(new CounterStarted(id));
            return counter;
        }

        public void Increment(int by) => Raise(new CounterIncremented(Id, by));

        public void Retire() => MarkForArchive();

        public void Apply(CounterStarted e) => Id = e.CounterId;

        public void Apply(CounterIncremented e) => Value += e.By;

        protected override void When(IDomainEvent @event)
        {
            switch (@event)
            {
                case CounterStarted e: Apply(e); break;
                case CounterIncremented e: Apply(e); break;
                default: throw new InvalidOperationException($"Unhandled event {@event.GetType().Name}");
            }
        }
    }

    [Fact]
    public void Raise_applies_records_and_registers_the_event()
    {
        var id = Guid.NewGuid();
        var counter = Counter.Start(id);
        counter.Increment(3);

        Assert.Equal(id, counter.Id);
        Assert.Equal(3, counter.Value);
        Assert.Equal(2, counter.UncommittedEvents.Count);
        Assert.Equal(2, counter.DomainEvents.Count); // same events are the in-process domain events
        Assert.IsType<CounterIncremented>(counter.UncommittedEvents[1]);
    }

    [Fact]
    public void Version_is_zero_for_a_new_aggregate_and_settable_by_the_store()
    {
        var counter = Counter.Start(Guid.NewGuid());
        Assert.Equal(0, counter.Version);

        ((IEventSourcedAggregate)counter).SetVersion(7);
        Assert.Equal(7, counter.Version);
    }

    [Fact]
    public void ClearUncommittedEvents_leaves_domain_events_for_the_dispatcher()
    {
        var counter = Counter.Start(Guid.NewGuid());

        ((IEventSourcedAggregate)counter).ClearUncommittedEvents();

        Assert.Empty(counter.UncommittedEvents);
        Assert.Single(counter.DomainEvents);
    }

    [Fact]
    public void MarkForArchive_flags_the_aggregate()
    {
        var counter = Counter.Start(Guid.NewGuid());
        Assert.False(counter.IsMarkedForArchive);

        counter.Retire();

        Assert.True(counter.IsMarkedForArchive);
    }

    [Fact]
    public void Raise_rejects_null()
    {
        var counter = Counter.Start(Guid.NewGuid());
        var raise = typeof(EventSourcedAggregate).GetMethod("Raise",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => raise.Invoke(counter, [null]));

        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }
}
