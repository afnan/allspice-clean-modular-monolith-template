using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Mediator;
using Microsoft.Extensions.Logging;
using DomainChannel = AllSpice.CleanModularMonolith.Notifications.Domain.Enums.NotificationChannel;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Wolverine handler that converts notification integration events into internal queue commands.
/// </summary>
public static class NotificationRequestedIntegrationEventConsumer
{
    // Idempotency: redelivery of the SAME envelope (the realistic duplicate — handler succeeded but the ack
    // was lost, or a transient retry) is deduped by Wolverine's durable inbox / durable local queues
    // (configured in the gateway), which track and skip already-processed envelope IDs. This is best-effort
    // across the main(messagingdb)/ancillary(notificationsdb) store boundary — the same accepted cross-store
    // trade-off as the outbox model. If stronger or cross-envelope dedup is ever required (same logical event
    // under two envelope IDs), add an explicit processed-event store keyed on EventId — tracked in TODOS.md.
    public static async Task HandleAsync(
        NotificationRequestedIntegrationEvent message,
        IMediator mediator,
        ILogger<NotificationRequestedIntegrationEvent> logger,
        CancellationToken cancellationToken)
    {
        var channel = MapChannel(message.Channel);

        var command = new QueueNotificationCommand(
            message.RecipientUserId,
            message.RecipientEmail,
            message.RecipientPhoneNumber,
            channel,
            message.Subject,
            message.Body,
            message.TemplateKey,
            message.Metadata,
            message.ScheduledSendUtc,
            message.CorrelationId);

        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            // Permanent failures (validation, not-found, authorization) will fail identically on
            // every retry, so retrying only wastes the budget before dead-lettering. Log and drop.
            if (result.Status is ResultStatus.Invalid or ResultStatus.NotFound
                or ResultStatus.Forbidden or ResultStatus.Unauthorized)
            {
                var errors = result.Status == ResultStatus.Invalid
                    ? string.Join("; ", result.ValidationErrors.Select(e => e.ErrorMessage))
                    : string.Join("; ", result.Errors);
                logger.LogError(
                    "Dropping notification request for {Recipient}: permanent failure ({Status}): {Errors}",
                    message.RecipientEmail, result.Status, errors);
                return;
            }

            // Transient failures (e.g. database unavailable) — surface a typed transient exception so
            // the gateway's Wolverine retry policy retries, rather than dead-lettering immediately.
            throw new TransientMessagingException(
                $"Failed to queue notification for {message.RecipientEmail}: {string.Join("; ", result.Errors)}");
        }
    }

    private static DomainChannel MapChannel(NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => DomainChannel.Email,
            NotificationChannel.Sms => DomainChannel.Sms,
            NotificationChannel.InApp => DomainChannel.InApp,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, $"Unknown notification channel: {channel}")
        };
}
