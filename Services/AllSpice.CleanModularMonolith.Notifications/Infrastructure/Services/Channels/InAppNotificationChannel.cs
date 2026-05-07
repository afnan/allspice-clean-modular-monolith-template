using Ardalis.GuardClauses;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.RealTime;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;

/// <summary>
/// Notification channel that pushes messages to connected clients via SignalR.
/// </summary>
/// <remarks>
/// Contract: <see cref="NotificationRecipient.UserId"/> MUST be the Keycloak external
/// ID of the recipient (the value stored in the JWT <c>sub</c> claim and used as the
/// SignalR group key in <see cref="AppHub"/>). Local Guid → external resolution must
/// happen at the boundary where the notification is queued, not here. Keycloak user
/// IDs are themselves Guid-formatted, so this channel cannot reliably distinguish
/// "local user Guid that needs resolving" from "external user Guid that's already
/// resolved" — accepting both modes used to silently mis-route notifications when
/// the wrong format reached this layer.
/// </remarks>
public sealed class InAppNotificationChannel : INotificationChannel
{
    private readonly IRealtimePublisher _realtimePublisher;
    private readonly ILogger<InAppNotificationChannel> _logger;

    public InAppNotificationChannel(
        IRealtimePublisher realtimePublisher,
        ILogger<InAppNotificationChannel> logger)
    {
        _realtimePublisher = realtimePublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.InApp;

    /// <inheritdoc />
    public async Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(notification);
        Guard.Against.Null(notification.Recipient, nameof(notification.Recipient));

        if (string.IsNullOrWhiteSpace(notification.Recipient.UserId))
        {
            _logger.LogWarning("In-app notification {NotificationId} has empty recipient UserId; skipping.", notification.Id);
            return Result.Error("In-app notifications require Recipient.UserId to be the Keycloak external ID.");
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

        await _realtimePublisher.PublishNotificationAsync(notification.Recipient.UserId, dto, cancellationToken);

        _logger.LogInformation(
            "In-app notification {NotificationId} delivered to user {ExternalUserId}.",
            notification.Id,
            notification.Recipient.UserId);

        return Result.Success();
    }
}
