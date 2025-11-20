namespace AllSpice.CleanModularMonolith.ApiGateway.RealTime;

/// <summary>
/// Publishes real-time notifications to connected SignalR clients.
/// </summary>
public sealed class RealtimePublisher : IRealtimePublisher
{
    private readonly IHubContext<AppHub, IAppHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimePublisher"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context used to broadcast messages.</param>
    public RealtimePublisher(IHubContext<AppHub, IAppHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

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


