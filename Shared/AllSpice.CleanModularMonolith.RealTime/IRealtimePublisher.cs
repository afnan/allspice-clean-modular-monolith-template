namespace AllSpice.CleanModularMonolith.RealTime;

/// <summary>
/// Abstraction for publishing real-time events to SignalR clients.
/// </summary>
public interface IRealtimePublisher
{
    /// <summary>
    /// Sends an event payload to the group associated with the specified user.
    /// </summary>
    /// <param name="externalUserId">The external (Keycloak) subject id — the same id AppHub joins its per-user group by.</param>
    Task PublishToUserAsync(string externalUserId, string topic, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a strongly-typed notification payload to a user.
    /// </summary>
    /// <param name="externalUserId">The external (Keycloak) subject id — the same id AppHub joins its per-user group by.</param>
    Task PublishNotificationAsync(string externalUserId, NotificationRealtimeDto notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts an event payload to the given SignalR group.
    /// </summary>
    Task PublishToGroupAsync(string groupName, string topic, object payload, CancellationToken cancellationToken = default);
}

