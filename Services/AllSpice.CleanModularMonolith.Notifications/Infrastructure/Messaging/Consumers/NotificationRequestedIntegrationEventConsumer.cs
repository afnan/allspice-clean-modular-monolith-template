using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using Mediator;
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

        await mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Maps contract channels to domain channel instances.
    /// </summary>
    /// <param name="channel">The channel specified in the integration event.</param>
    /// <returns>A domain channel enumeration value.</returns>
    private static DomainChannel MapChannel(NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => DomainChannel.Email,
            NotificationChannel.Sms => DomainChannel.Sms,
            NotificationChannel.InApp => DomainChannel.InApp,
            _ => DomainChannel.Email
        };
}
