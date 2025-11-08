using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Events;

public sealed class NotificationQueuedDomainEvent : DomainEventBase
{
    public NotificationQueuedDomainEvent(Notification notification)
    {
        Notification = notification;
    }

    public Notification Notification { get; }
}


