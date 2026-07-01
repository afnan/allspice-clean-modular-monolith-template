using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;
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

    // NOTE: There are deliberately NO client-invokable Join/Leave methods. A connection is bound to its own
    // "user:{subjectId}" group automatically in OnConnectedAsync from the authenticated claims — the caller
    // never supplies the target id. Previous JoinUser(userId)/JoinGroup(groupName) methods took a
    // caller-supplied identifier and added the connection to ANY group with no ownership check, letting any
    // authenticated client subscribe to another user's (or an arbitrary) notification stream (broken access
    // control / IDOR). If a project needs additional group memberships (e.g. tenant/role channels), add hub
    // methods that authorize the requested group server-side (verify it belongs to Context.User) before
    // calling Groups.AddToGroupAsync — never trust the client-supplied name.

    /// <summary>
    /// Resolves the external subject id from the connection's claims (the SignalR/JWT boundary, where the
    /// external id is the correct identity). Delegates to the shared <see cref="ClaimsPrincipalExtensions.GetSubjectId"/>.
    /// </summary>
    private string? GetUserId() => Context.User?.GetSubjectId();

    /// <summary>
    /// Builds the normalized user group name for a given user.
    /// </summary>
    private static string BuildUserGroup(string userId) => $"user:{userId}";

    /// <summary>
    /// Returns the normalized user group name for inbound/outbound references.
    /// </summary>
    public static string GetUserGroup(string userId) => BuildUserGroup(userId);
}


