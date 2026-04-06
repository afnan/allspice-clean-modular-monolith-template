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
/// Resolves local user IDs to Keycloak external IDs for SignalR group matching.
/// </summary>
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

        // Resolve local user ID to Keycloak external ID for SignalR group matching
        var targetUserId = notification.Recipient.UserId;

        if (Guid.TryParse(targetUserId, out var localUserId))
        {
            var externalId = await _userExternalIdResolver.GetExternalIdByLocalIdAsync(localUserId, cancellationToken);

            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("Could not resolve external ID for user {UserId}", targetUserId);
                return Result.Error("Could not resolve external user ID for in-app notification.");
            }

            targetUserId = externalId;
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

        await _realtimePublisher.PublishNotificationAsync(targetUserId, dto, cancellationToken);

        _logger.LogInformation(
            "In-app notification {NotificationId} delivered to user {UserId}.",
            notification.Id,
            targetUserId);

        return Result.Success();
    }
}
