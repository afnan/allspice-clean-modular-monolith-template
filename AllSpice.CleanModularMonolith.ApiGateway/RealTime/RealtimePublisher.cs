namespace AllSpice.CleanModularMonolith.ApiGateway.RealTime;

/// <summary>
/// Publishes real-time notifications to connected SignalR clients.
/// </summary>
public sealed class RealtimePublisher(IHubContext<AppHub, IAppHubClient> hubContext) : IRealtimePublisher
{
    private readonly IHubContext<AppHub, IAppHubClient> _hubContext = hubContext;

    /// <inheritdoc />
    public Task PublishToUserAsync(string userId, string topic, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = AppHub.GetUserGroup(userId);
        return _hubContext.Clients.Group(groupName).ReceiveEvent(topic, payload);
    }

    /// <inheritdoc />
    public Task PublishNotificationAsync(string userId, NotificationRealtimeDto notification, CancellationToken cancellationToken = default)
    {
        var groupName = AppHub.GetUserGroup(userId);
        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    /// <inheritdoc />
    public Task PublishToGroupAsync(string groupName, string topic, object payload, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.Group(groupName).ReceiveEvent(topic, payload);
    }
}


