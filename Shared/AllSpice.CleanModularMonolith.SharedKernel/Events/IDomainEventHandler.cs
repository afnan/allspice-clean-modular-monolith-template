using Mediator;

namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
}


