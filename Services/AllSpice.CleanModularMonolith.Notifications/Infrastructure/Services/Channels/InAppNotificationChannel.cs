using Ardalis.GuardClauses;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.RealTime;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;

/// <summary>
/// Notification channel that pushes messages to connected clients via SignalR.
/// </summary>
/// <remarks>
/// <para>Identity contract:
/// <list type="bullet">
/// <item><see cref="NotificationRecipient.UserId"/> MUST be the local user UUID
/// (the value of <c>User.Id</c>). The local UUID is the canonical user identity
/// used everywhere in the application.</item>
/// <item>The Keycloak external ID is reserved for Keycloak-specific operations
/// (admin API calls, JWT inspection). It also happens to be the SignalR group
/// key today because <see cref="AppHub"/> reads it from the JWT <c>sub</c>
/// claim — this channel resolves local UUID → external ID at the boundary.</item>
/// </list></para>
/// <para>This channel previously accepted either format and tried to
/// <see cref="Guid.TryParse"/> the input to decide whether to resolve. Keycloak
/// user IDs are themselves Guid-formatted, so the heuristic was unreliable —
/// callers passing an external Guid would mis-route. Contract is now strict:
/// always local UUID in, always resolve out.</para>
/// </remarks>
public sealed class InAppNotificationChannel : INotificationChannel
{
    private readonly IRealtimePublisher _realtimePublisher;
    private readonly IUserExternalIdResolver _userExternalIdResolver;
    private readonly ILogger<InAppNotificationChannel> _logger;

    public InAppNotificationChannel(
        IRealtimePublisher realtimePublisher,
        IUserExternalIdResolver userExternalIdResolver,
        ILogger<InAppNotificationChannel> logger)
    {
        _realtimePublisher = realtimePublisher;
        _userExternalIdResolver = userExternalIdResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.InApp;

    /// <inheritdoc />
    public async Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(notification);
        Guard.Against.Null(notification.Recipient, nameof(notification.Recipient));

        if (!Guid.TryParse(notification.Recipient.UserId, out var localUserId))
        {
            _logger.LogWarning(
                "In-app notification {NotificationId} has non-Guid Recipient.UserId '{UserId}'; expected local user UUID.",
                notification.Id,
                notification.Recipient.UserId);
            return Result.Error("Recipient.UserId must be the local user UUID for in-app notifications.");
        }

        var externalId = await _userExternalIdResolver.GetExternalIdByLocalIdAsync(localUserId, cancellationToken);
        if (string.IsNullOrEmpty(externalId))
        {
            _logger.LogWarning(
                "In-app notification {NotificationId}: could not resolve external ID for local user {LocalUserId}.",
                notification.Id,
                localUserId);
            return Result.Error("Could not resolve external user ID for in-app notification.");
        }

        var metadata = notification.GetMetadata();
        var dto = new NotificationRealtimeDto(
            notification.Id,
            content.Subject,
            content.Body,
            content.IsHtml,
            notification.CreatedUtc,
            notification.CorrelationId,
            metadata);

        await _realtimePublisher.PublishNotificationAsync(externalId, dto, cancellationToken);

        _logger.LogInformation(
            "In-app notification {NotificationId} delivered to local user {LocalUserId} (external {ExternalId}).",
            notification.Id,
            localUserId,
            externalId);

        return Result.Success();
    }
}
