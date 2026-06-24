using Mediator;
using Microsoft.Extensions.Logging;
using AllSpice.CleanModularMonolith.Notifications.Domain.Events;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Events;

public sealed class NotificationQueuedDomainEventHandler(ILogger<NotificationQueuedDomainEventHandler> logger)
    : INotificationHandler<NotificationQueuedDomainEvent>
{
    private readonly ILogger<NotificationQueuedDomainEventHandler> _logger = logger;

    public ValueTask Handle(NotificationQueuedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Notification {NotificationId} queued for user {UserId} via channel {Channel}.",
            notification.NotificationId,
            notification.RecipientUserId,
            notification.ChannelName);

        return ValueTask.CompletedTask;
    }
}
