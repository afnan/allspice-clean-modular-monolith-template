using Mediator;

namespace AllSpice.CleanModularMonolith.SharedKernel.Events;

/// <summary>
/// Marker interface for domain events dispatched through Mediator.
/// </summary>
public interface IDomainEvent : INotification
{
}


