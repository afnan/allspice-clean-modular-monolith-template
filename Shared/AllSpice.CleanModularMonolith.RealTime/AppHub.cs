using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AllSpice.CleanModularMonolith.RealTime;

public interface IAppHubClient
{
    Task ReceiveEvent(string topic, object payload);

    Task ReceiveNotification(NotificationRealtimeDto notification);
}

[Authorize]
public sealed class AppHub : Hub<IAppHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinUser(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
    }

    public async Task LeaveUser(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    private string? GetUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? Context.User?.FindFirst("sub")?.Value
           ?? Context.User?.FindFirst("oid")?.Value;

    private static string BuildUserGroup(string userId) => $"user:{userId}";

    public static string GetUserGroup(string userId) => BuildUserGroup(userId);
}


