using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Coordinates notification delivery across the registered delivery channels.
/// </summary>
public sealed class NotificationDispatcher(
    INotificationRepository notificationRepository,
    NotificationsDbContext dbContext,
    IEnumerable<INotificationChannel> channels,
    INotificationPreferenceRepository preferenceRepository,
    INotificationContentBuilder contentBuilder,
    IDbContextOutbox outbox,
    IOptions<NotificationDispatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly NotificationsDbContext _dbContext = dbContext;
    private readonly IEnumerable<INotificationChannel> _channels = channels;
    private readonly INotificationPreferenceRepository _preferenceRepository = preferenceRepository;
    private readonly INotificationContentBuilder _contentBuilder = contentBuilder;
    private readonly IDbContextOutbox _outbox = outbox;
    private readonly NotificationDispatcherOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<NotificationDispatcher> _logger = logger;

    // The dispatcher runs in a BackgroundService scope, NOT through an ITransactional Mediator command,
    // so TransactionBehavior never runs for it. Repositories only STAGE writes (see EfRepository), so the
    // dispatcher owns its own unit of work: stage via the repository, then flush via the module DbContext.
    // Each notification is persisted independently so a mid-batch failure doesn't lose prior progress.
    //
    // Returns false when the write loses an optimistic-concurrency race (Notification.LastUpdatedUtc is the
    // concurrency token): another dispatcher replica claimed/reclaimed/terminalized the same row first. The
    // stale instance is detached so it isn't retried, and the caller skips the row.
    private async Task<bool> TryPersistAsync(Notification notification, CancellationToken cancellationToken)
    {
        await _notificationRepository.UpdateAsync(notification, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.Entry(notification).State = EntityState.Detached;
            _logger.LogDebug(
                "Notification {NotificationId} was concurrently modified by another replica; skipping.",
                notification.Id);
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Multi-replica safe: marking a notification <c>Dispatched</c> is a CONDITIONAL update guarded by the
    /// <c>LastUpdatedUtc</c> optimistic-concurrency token (see <see cref="TryPersistAsync"/>). When several
    /// dispatcher replicas poll the same batch, exactly one wins the claim per row; the losers get a
    /// concurrency conflict and skip — so a row is never sent by two replicas. (An atomic
    /// <c>SELECT ... FOR UPDATE SKIP LOCKED</c> claim is an equivalent Postgres-native alternative; the
    /// optimistic approach is used here because it is provider-agnostic and preserves the per-row flow.)
    ///
    /// Delivery is at-least-once: a crash AFTER a successful send but BEFORE <c>Delivered</c> commits leaves
    /// the row in <c>Dispatched</c>; the reclaim path re-sends it, which can duplicate a real delivery. The
    /// mitigation is channel idempotency — see <see cref="INotificationChannel"/>.
    /// </remarks>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow();
        var reclaimBefore = utcNow.AddSeconds(-_options.ReclaimAfterSeconds);
        var specification = new DueNotificationsSpecification(utcNow, reclaimBefore);
        var pendingNotifications = await _notificationRepository.ListAsync(specification, cancellationToken);

        if (pendingNotifications.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var notification in pendingNotifications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reclaimed row (Status == Dispatched) that has already exhausted its attempt budget: do NOT
            // re-send (that attempt was already counted before the crash). Terminalize it to Failed so it
            // doesn't sit in Dispatched forever. NOTE (at-least-once): a row stranded AFTER a successful
            // send but before MarkDelivered committed will, if still within budget, be re-sent on reclaim —
            // i.e. reclaim can duplicate a real delivery. Channel-level idempotency is the true mitigation.
            if (notification.Status == NotificationStatus.Dispatched &&
                notification.AttemptCount >= Notification.MaxDeliveryAttempts)
            {
                notification.HandleFailure("Stranded in Dispatched after exhausting delivery attempts.", utcNow);
                if (await TryPersistAsync(notification, cancellationToken))
                {
                    _logger.LogWarning("Notification {NotificationId} terminalized: stranded in Dispatched at max attempts.", notification.Id);
                }

                continue;
            }

            // Preferences are keyed by local user UUID. Recipient.UserId has three cases:
            //   - empty/whitespace  -> a userless transactional/system notification (e.g. an invitation
            //     email to someone with no account yet). No opt-out preferences apply, so send it.
            //   - a valid local Guid -> evaluate the user/channel opt-out preference.
            //   - non-empty but not a Guid -> malformed. Fail closed: don't risk delivering to a user
            //     whose opt-out we can't evaluate (a privacy/compliance hazard).
            if (string.IsNullOrWhiteSpace(notification.Recipient.UserId))
            {
                // Userless recipient — nothing to check; fall through to dispatch.
            }
            else if (Guid.TryParse(notification.Recipient.UserId, out var localUserId))
            {
                var preference = await _preferenceRepository.GetByUserAndChannelAsync(localUserId, notification.Channel, cancellationToken);
                if (preference is not null && !preference.IsEnabled)
                {
                    _logger.LogInformation("Notification {NotificationId} skipped due to user/channel preference.", notification.Id);
                    notification.Cancel(utcNow, "User has opted out of this channel.");
                    await TryPersistAsync(notification, cancellationToken);
                    continue;
                }
            }
            else
            {
                _logger.LogWarning(
                    "Notification {NotificationId} has non-Guid Recipient.UserId '{UserId}'; cannot evaluate preferences — failing closed.",
                    notification.Id,
                    notification.Recipient.UserId);
                notification.HandleFailure("Invalid Recipient.UserId format; cannot evaluate notification preferences.", utcNow);
                await TryPersistAsync(notification, cancellationToken);
                continue;
            }

            notification.RecordAttempt(utcNow);

            // Claim: mark Dispatched and persist BEFORE sending. This is the optimistic-concurrency claim —
            // if another replica already claimed/reclaimed this row, the persist loses the race and we skip
            // (no double dispatch).
            notification.MarkDispatched(utcNow);
            if (!await TryPersistAsync(notification, cancellationToken))
            {
                // Lost the claim to another replica — TryPersistAsync already logged the skip.
                continue;
            }

            var contentResult = await _contentBuilder.BuildAsync(notification, cancellationToken);
            if (!contentResult.IsSuccess)
            {
                var errorDetail = contentResult.Errors.FirstOrDefault() ?? "Failed to build notification content.";
                notification.HandleFailure(errorDetail, utcNow);
                await TryPersistAsync(notification, cancellationToken);
                _logger.LogWarning("Notification {NotificationId} content build failed: {Error}", notification.Id, errorDetail);
                continue;
            }

            var channelHandler = _channels.FirstOrDefault(handler => handler.Channel == notification.Channel);

            if (channelHandler is null)
            {
                notification.HandleFailure($"No notification channel registered for '{notification.Channel.Name}'.", utcNow);
                await TryPersistAsync(notification, cancellationToken);
                _logger.LogWarning("No channel handler for notification {NotificationId} ({Channel})", notification.Id, notification.Channel.Name);
                continue;
            }

            try
            {
                var sendResult = await channelHandler.SendAsync(notification, contentResult.Value, cancellationToken);

                if (sendResult.IsSuccess)
                {
                    var deliveryEvent = new NotificationDeliveredIntegrationEvent(
                        Guid.NewGuid(),
                        notification.Id,
                        notification.Channel.Name,
                        notification.Recipient.UserId,
                        notification.CorrelationId,
                        notification.AttemptCount,
                        utcNow);

                    // Atomic: the Delivered status and the integration-event envelope commit in ONE
                    // transaction via the module's co-located outbox — no fire-and-forget. A crash can't
                    // deliver the event without persisting Delivered, or persist Delivered without the event.
                    await using var deliveredTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                    notification.MarkDelivered(utcNow);
                    await _notificationRepository.UpdateAsync(notification, cancellationToken);
                    _outbox.Enroll(_dbContext);
                    await _outbox.PublishAsync(deliveryEvent);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await deliveredTx.CommitAsync(cancellationToken);
                    processed++;
                    _logger.LogInformation("Notification {NotificationId} delivered via {Channel}", notification.Id, notification.Channel.Name);

                    // The Delivered row and its outbox envelope are already durably committed. Flushing to
                    // send promptly is best-effort ONLY: a transport blip here must NOT fall through to the
                    // outer catch, which would call HandleFailure and revert an already-delivered row (a
                    // duplicate send). Wolverine's recovery sweep delivers the envelope if this flush fails.
                    try
                    {
                        await _outbox.FlushOutgoingMessagesAsync();
                    }
                    catch (Exception flushEx)
                    {
                        _logger.LogWarning(flushEx,
                            "Post-commit outbox flush failed for notification {NotificationId}; the durable recovery loop will deliver it.",
                            notification.Id);
                    }
                }
                else
                {
                    var errorDetail = sendResult.Errors.FirstOrDefault() ?? "Unknown delivery error";
                    notification.HandleFailure(errorDetail, utcNow);
                    await TryPersistAsync(notification, cancellationToken);
                    _logger.LogWarning("Notification {NotificationId} failed via {Channel}: {Error}", notification.Id, notification.Channel.Name, errorDetail);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // The send succeeded, but another replica reclaimed the row before Delivered could commit
                // (the send outran the reclaim window). We can't record Delivered; that replica will retry —
                // an accepted at-least-once duplicate. Channels must be idempotent on notification.Id.
                _dbContext.Entry(notification).State = EntityState.Detached;
                _logger.LogWarning(
                    "Notification {NotificationId} was delivered but concurrently reclaimed; a duplicate send may occur (at-least-once).",
                    notification.Id);
            }
            catch (Exception ex)
            {
                notification.HandleFailure(ex.Message, utcNow);
                await TryPersistAsync(notification, cancellationToken);
                _logger.LogError(ex, "Error dispatching notification {NotificationId}", notification.Id);
            }
        }

        return processed;
    }
}
