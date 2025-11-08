namespace AllSpice.CleanModularMonolith.RealTime;

public interface IRealtimePublisher
{
    Task PublishToUserAsync(string userId, string topic, object payload, CancellationToken cancellationToken = default);

    Task PublishNotificationAsync(string userId, NotificationRealtimeDto notification, CancellationToken cancellationToken = default);

    Task PublishToGroupAsync(string groupName, string topic, object payload, CancellationToken cancellationToken = default);
}


