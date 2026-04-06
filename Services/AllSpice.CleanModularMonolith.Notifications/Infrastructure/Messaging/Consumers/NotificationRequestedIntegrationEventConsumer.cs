using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using Mediator;
using Microsoft.Extensions.Logging;
using DomainChannel = AllSpice.CleanModularMonolith.Notifications.Domain.Enums.NotificationChannel;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Wolverine handler that converts notification integration events into internal queue commands.
/// </summary>
public static class NotificationRequestedIntegrationEventConsumer
{
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
            // Throw so Wolverine's retry policies kick in for transient failures.
            // The durable outbox will preserve the message for retry.
            throw new InvalidOperationException(
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
