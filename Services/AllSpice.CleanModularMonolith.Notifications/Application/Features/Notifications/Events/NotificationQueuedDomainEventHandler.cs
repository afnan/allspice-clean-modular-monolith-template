using Mediator;
using Microsoft.Extensions.Logging;
using AllSpice.CleanModularMonolith.Notifications.Domain.Events;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Events;

public sealed class NotificationQueuedDomainEventHandler : INotificationHandler<NotificationQueuedDomainEvent>
{
    private readonly ILogger<NotificationQueuedDomainEventHandler> _logger;

    public NotificationQueuedDomainEventHandler(ILogger<NotificationQueuedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(NotificationQueuedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Notification {NotificationId} queued for user {UserId} via channel {Channel}.",
            notification.Notification.Id,
            notification.Notification.Recipient.UserId,
            notification.Notification.Channel.Name);

        return ValueTask.CompletedTask;
    }
}


