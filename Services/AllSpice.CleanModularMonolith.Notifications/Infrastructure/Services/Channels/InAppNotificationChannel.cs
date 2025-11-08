using Ardalis.GuardClauses;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.RealTime;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;

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

    public NotificationChannel Channel => NotificationChannel.InApp;

    public async Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(notification);
        Guard.Against.Null(notification.Recipient, nameof(notification.Recipient));

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
            "In-app notification {NotificationId} delivered to user {UserId}.",
            notification.Id,
            notification.Recipient.UserId);

        return Result.Success();
    }
}


