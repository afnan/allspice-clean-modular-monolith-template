using AllSpice.CleanModularMonolith.RealTime;
using Microsoft.AspNetCore.SignalR;

namespace AllSpice.CleanModularMonolith.ApiGateway.RealTime;

public sealed class RealtimePublisher : IRealtimePublisher
{
    private readonly IHubContext<AppHub, IAppHubClient> _hubContext;

    public RealtimePublisher(IHubContext<AppHub, IAppHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishToUserAsync(string userId, string topic, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = AppHub.GetUserGroup(userId);
        return _hubContext.Clients.Group(groupName).ReceiveEvent(topic, payload);
    }

    public Task PublishNotificationAsync(string userId, NotificationRealtimeDto notification, CancellationToken cancellationToken = default)
    {
        var groupName = AppHub.GetUserGroup(userId);
        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    public Task PublishToGroupAsync(string groupName, string topic, object payload, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.Group(groupName).ReceiveEvent(topic, payload);
    }
}


