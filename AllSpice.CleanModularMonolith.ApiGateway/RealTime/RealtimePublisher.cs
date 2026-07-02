namespace AllSpice.CleanModularMonolith.ApiGateway.RealTime;

/// <summary>
/// Publishes real-time notifications to connected SignalR clients.
/// </summary>
public sealed class RealtimePublisher(IHubContext<AppHub, IAppHubClient> hubContext) : IRealtimePublisher
{
    private readonly IHubContext<AppHub, IAppHubClient> _hubContext = hubContext;

    /// <summary>
    /// Sends an event payload to the SignalR group for the specified user.
    /// </summary>
    /// <param name="externalUserId">
    /// The user's EXTERNAL Keycloak subject id (the <c>sub</c> claim) — NOT the local user UUID. It MUST match
    /// the id <see cref="AppHub"/> uses to join a connection to its <c>user:{externalUserId}</c> group (see
    /// <see cref="AppHub.GetUserGroup(string)"/>); passing a local UUID targets a non-existent group and the
    /// message is silently dropped. No local-to-external resolution is performed here.
    /// </param>
    /// <param name="topic">The event topic/name delivered to the client.</param>
    /// <param name="payload">The event payload delivered to the client.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PublishToUserAsync(string externalUserId, string topic, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = AppHub.GetUserGroup(externalUserId);
        return _hubContext.Clients.Group(groupName).ReceiveEvent(topic, payload);
    }

    /// <summary>
    /// Sends a strongly-typed notification payload to the specified user's SignalR group.
    /// </summary>
    /// <param name="externalUserId">
    /// The user's EXTERNAL Keycloak subject id (the <c>sub</c> claim) — NOT the local user UUID. It MUST match
    /// the id <see cref="AppHub"/> uses to join a connection to its <c>user:{externalUserId}</c> group (see
    /// <see cref="AppHub.GetUserGroup(string)"/>); passing a local UUID targets a non-existent group and the
    /// message is silently dropped.
    /// </param>
    /// <param name="notification">The notification payload delivered to the client.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task PublishNotificationAsync(string externalUserId, NotificationRealtimeDto notification, CancellationToken cancellationToken = default)
    {
        var groupName = AppHub.GetUserGroup(externalUserId);
        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    /// <inheritdoc />
    public Task PublishToGroupAsync(string groupName, string topic, object payload, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.Group(groupName).ReceiveEvent(topic, payload);
    }
}


