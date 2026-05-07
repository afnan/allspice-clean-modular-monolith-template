using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using Microsoft.Extensions.Logging;
using Wolverine;

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
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NotificationDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDispatcher"/> class.
    /// </summary>
    /// <param name="notificationRepository">Repository used to load and update notification records.</param>
    /// <param name="channels">The set of delivery channel handlers available.</param>
    /// <param name="preferenceRepository">Repository for recipient notification preferences.</param>
    /// <param name="contentBuilder">Service that renders notification content.</param>
    /// <param name="messageBus">Wolverine message bus used for event publication.</param>
    /// <param name="logger">Logger used to record dispatcher activity.</param>
    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        IEnumerable<INotificationChannel> channels,
        INotificationPreferenceRepository preferenceRepository,
        INotificationContentBuilder contentBuilder,
        IMessageBus messageBus,
        ILogger<NotificationDispatcher> logger)
    {
        _notificationRepository = notificationRepository;
        _channels = channels;
        _preferenceRepository = preferenceRepository;
        _contentBuilder = contentBuilder;
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// KNOWN LIMITATION (multi-replica): the SELECT (Pending notifications) and the
    /// subsequent UPDATE (mark Dispatched) are not atomic, so two replicas of the
    /// gateway can both grab the same batch and double-send. The "mark Dispatched
    /// before SendAsync" pattern below shrinks the window but does not close it.
    ///
    /// The proper fix when scaling beyond a single replica is an atomic claim via
    /// `UPDATE notifications SET status='Dispatched' ... WHERE id IN (SELECT ...
    /// FOR UPDATE SKIP LOCKED) RETURNING *` — implementable as raw SQL on the
    /// repository or by adding a `ClaimedByDispatcherId` column. Document then
    /// implement when multi-replica deployment is on the roadmap.
    /// </remarks>
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

            // Preferences are keyed by local user UUID. Recipient.UserId follows the same
            // convention (see InAppNotificationChannel xmldoc). If we can't parse it, skip
            // the preference check rather than failing — the notification will dispatch
            // with the channel's default opt-in state.
            if (Guid.TryParse(notification.Recipient.UserId, out var localUserId))
            {
                var preference = await _preferenceRepository.GetByUserAndChannelAsync(localUserId, notification.Channel, cancellationToken);
                if (preference is not null && !preference.IsEnabled)
                {
                    _logger.LogInformation("Notification {NotificationId} skipped due to user/channel preference.", notification.Id);
                    continue;
                }
            }
            else
            {
                _logger.LogWarning(
                    "Notification {NotificationId} has non-Guid Recipient.UserId '{UserId}'; preference check skipped.",
                    notification.Id,
                    notification.Recipient.UserId);
            }

            notification.RecordAttempt();

            // Mark as Dispatched and persist BEFORE sending to prevent duplicate dispatch
            // by the background service polling concurrently.
            notification.MarkDispatched();
            await _notificationRepository.UpdateAsync(notification, cancellationToken);

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

                    await _messageBus.PublishAsync(deliveryEvent);

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
