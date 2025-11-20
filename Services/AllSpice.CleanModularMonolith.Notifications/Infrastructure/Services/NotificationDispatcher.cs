using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Coordinates notification delivery across the registered delivery channels.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly INotificationContentBuilder _contentBuilder;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<NotificationDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDispatcher"/> class.
    /// </summary>
    /// <param name="notificationRepository">Repository used to load and update notification records.</param>
    /// <param name="channels">The set of delivery channel handlers available.</param>
    /// <param name="preferenceRepository">Repository for recipient notification preferences.</param>
    /// <param name="contentBuilder">Service that renders notification content.</param>
    /// <param name="publishEndpoint">MassTransit endpoint used for event publication.</param>
    /// <param name="logger">Logger used to record dispatcher activity.</param>
    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        IEnumerable<INotificationChannel> channels,
        INotificationPreferenceRepository preferenceRepository,
        INotificationContentBuilder contentBuilder,
        IPublishEndpoint publishEndpoint,
        ILogger<NotificationDispatcher> logger)
    {
        _notificationRepository = notificationRepository;
        _channels = channels;
        _preferenceRepository = preferenceRepository;
        _contentBuilder = contentBuilder;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var specification = new DueNotificationsSpecification(utcNow);
        var pendingNotifications = await _notificationRepository.ListAsync(specification, cancellationToken);

        if (pendingNotifications.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var notification in pendingNotifications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preference = await _preferenceRepository.GetByUserAndChannelAsync(notification.Recipient.UserId, notification.Channel, cancellationToken);
            if (preference is not null && !preference.IsEnabled)
            {
                _logger.LogInformation("Notification {NotificationId} skipped due to user/channel preference.", notification.Id);
                continue;
            }

            notification.RecordAttempt();

            var contentResult = await _contentBuilder.BuildAsync(notification, cancellationToken);
            if (!contentResult.IsSuccess)
            {
                var errorDetail = contentResult.Errors.FirstOrDefault() ?? "Failed to build notification content.";
                notification.HandleFailure(errorDetail);
                await _notificationRepository.UpdateAsync(notification, cancellationToken);
                _logger.LogWarning("Notification {NotificationId} content build failed: {Error}", notification.Id, errorDetail);
                continue;
            }

            var channelHandler = _channels.FirstOrDefault(handler => handler.Channel == notification.Channel);

            if (channelHandler is null)
            {
                notification.HandleFailure($"No notification channel registered for '{notification.Channel.Name}'.");
                await _notificationRepository.UpdateAsync(notification, cancellationToken);
                _logger.LogWarning("No channel handler for notification {NotificationId} ({Channel})", notification.Id, notification.Channel.Name);
                continue;
            }

            try
            {
                var sendResult = await channelHandler.SendAsync(notification, contentResult.Value, cancellationToken);

                if (sendResult.IsSuccess)
                {
                    notification.MarkDelivered();
                    await _notificationRepository.UpdateAsync(notification, cancellationToken);
                    processed++;

                    var deliveryEvent = new NotificationDeliveredIntegrationEvent(
                        Guid.NewGuid(),
                        notification.Id,
                        notification.Channel.Name,
                        notification.Recipient.UserId,
                        notification.CorrelationId,
                        notification.AttemptCount,
                        DateTimeOffset.UtcNow);

                    await _publishEndpoint.Publish(deliveryEvent, cancellationToken);

                    _logger.LogInformation("Notification {NotificationId} delivered via {Channel}", notification.Id, notification.Channel.Name);
                }
                else
                {
                    var errorDetail = sendResult.Errors.FirstOrDefault() ?? "Unknown delivery error";
                    notification.HandleFailure(errorDetail);
                    await _notificationRepository.UpdateAsync(notification, cancellationToken);
                    _logger.LogWarning("Notification {NotificationId} failed via {Channel}: {Error}", notification.Id, notification.Channel.Name, errorDetail);
                }
            }
            catch (Exception ex)
            {
                notification.HandleFailure(ex.Message);
                await _notificationRepository.UpdateAsync(notification, cancellationToken);
                _logger.LogError(ex, "Error dispatching notification {NotificationId}", notification.Id);
            }
        }

        return processed;
    }
}


