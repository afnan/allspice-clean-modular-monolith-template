using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Events;

public sealed class NotificationQueuedDomainEvent : DomainEventBase
{
    public NotificationQueuedDomainEvent(Guid notificationId, string recipientUserId, string channelName)
    {
        NotificationId = notificationId;
        RecipientUserId = recipientUserId;
        ChannelName = channelName;
    }

    public Guid NotificationId { get; }
    public string RecipientUserId { get; }
    public string ChannelName { get; }
}
