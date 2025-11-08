using Mediator;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

public sealed class MediatorDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatorDomainEventDispatcher> _logger;

    public MediatorDomainEventDispatcher(IMediator mediator, ILogger<MediatorDomainEventDispatcher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                _logger.LogDebug("Dispatching domain event {DomainEventName} ({EventId})", domainEvent.GetType().Name, (domainEvent as DomainEventBase)?.Id);
                await _mediator.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching domain event {DomainEventName}", domainEvent.GetType().Name);
                throw;
            }
        }
    }
}


