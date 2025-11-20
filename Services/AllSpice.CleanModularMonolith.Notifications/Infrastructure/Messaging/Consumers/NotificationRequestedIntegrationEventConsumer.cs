using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using MassTransit;
using Mediator;
using DomainChannel = AllSpice.CleanModularMonolith.Notifications.Domain.Enums.NotificationChannel;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer that converts notification integration events into internal queue commands.
/// </summary>
public sealed class NotificationRequestedIntegrationEventConsumer : IConsumer<NotificationRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationRequestedIntegrationEventConsumer"/> class.
    /// </summary>
    /// <param name="mediator">Mediator used to dispatch commands into the application layer.</param>
    public NotificationRequestedIntegrationEventConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<NotificationRequestedIntegrationEvent> context)
    {
        var message = context.Message;
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

        await _mediator.Send(command, context.CancellationToken);
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


