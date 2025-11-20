using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AllSpice.CleanModularMonolith.RealTime;

/// <summary>
/// Contract implemented by SignalR clients that subscribe to the application hub.
/// </summary>
public interface IAppHubClient
{
    /// <summary>
    /// Receives an arbitrary topic/payload event pushed from the server.
    /// </summary>
    Task ReceiveEvent(string topic, object payload);

    /// <summary>
    /// Receives a strongly-typed notification payload.
    /// </summary>
    Task ReceiveNotification(NotificationRealtimeDto notification);
}

/// <summary>
/// SignalR hub that manages user/group memberships and forwards events to clients.
/// </summary>
[Authorize]
public sealed class AppHub : Hub<IAppHubClient>
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Adds the current connection to the specified user's group.
    /// </summary>
    public async Task JoinUser(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
    }

    /// <summary>
    /// Removes the current connection from the specified user's group.
    /// </summary>
    public async Task LeaveUser(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
    }

    /// <summary>
    /// Adds the current connection to an arbitrary group.
    /// </summary>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Removes the current connection from an arbitrary group.
    /// </summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Resolves the user identifier from known claim types.
    /// </summary>
    private string? GetUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? Context.User?.FindFirst("sub")?.Value
           ?? Context.User?.FindFirst("oid")?.Value;

    /// <summary>
    /// Builds the normalized user group name for a given user.
    /// </summary>
    private static string BuildUserGroup(string userId) => $"user:{userId}";

    /// <summary>
    /// Returns the normalized user group name for inbound/outbound references.
    /// </summary>
    public static string GetUserGroup(string userId) => BuildUserGroup(userId);
}


