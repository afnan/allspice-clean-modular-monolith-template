namespace AllSpice.CleanModularMonolith.RealTime;

/// <summary>
/// Abstraction for publishing real-time events to SignalR clients.
/// </summary>
public interface IRealtimePublisher
{
    /// <summary>
    /// Sends an event payload to the group associated with the specified user.
    /// </summary>
    Task PublishToUserAsync(string userId, string topic, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a strongly-typed notification payload to a user.
    /// </summary>
    Task PublishNotificationAsync(string userId, NotificationRealtimeDto notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts an event payload to the given SignalR group.
    /// </summary>
    Task PublishToGroupAsync(string groupName, string topic, object payload, CancellationToken cancellationToken = default);
}

