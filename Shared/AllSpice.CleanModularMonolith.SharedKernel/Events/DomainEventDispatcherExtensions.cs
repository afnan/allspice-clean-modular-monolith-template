namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

public static class DomainEventDispatcherExtensions
{
    public static async Task DispatchDomainEventsAsync(this IDomainEventDispatcher dispatcher, IEnumerable<IHasDomainEvents> entities, CancellationToken cancellationToken = default)
    {
        var domainEvents = entities
            .SelectMany(entity => entity.TakeDomainEvents())
            .ToList();

        if (domainEvents.Count == 0)
        {
            return;
        }

        await dispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
    }
}


