using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Events;

public class DomainEventDispatcherExtensionsTests
{
    [Fact]
    public async Task DispatchDomainEventsAsync_PublishesRegisteredEvents()
    {
        // Arrange
        var dispatcher = new CapturingDispatcher();
        var aggregate = new SampleAggregate();

        aggregate.RaiseTestEvent();

        // Act
        await dispatcher.DispatchDomainEventsAsync(new[] { aggregate });

        // Assert
        Assert.Single(dispatcher.PublishedEvents);
        Assert.IsType<SampleDomainEvent>(dispatcher.PublishedEvents.Single());
    }

    private sealed class SampleAggregate : AggregateRoot
    {
        public void RaiseTestEvent() => RegisterDomainEvent(new SampleDomainEvent());
    }

    private sealed class SampleDomainEvent : DomainEventBase
    {
    }

    private sealed class CapturingDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> PublishedEvents { get; } = new();

        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            PublishedEvents.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }
}


