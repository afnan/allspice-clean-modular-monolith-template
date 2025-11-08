using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using MassTransit;
using Mediator;
using DomainChannel = AllSpice.CleanModularMonolith.Notifications.Domain.Enums.NotificationChannel;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;

public sealed class NotificationRequestedIntegrationEventConsumer : IConsumer<NotificationRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public NotificationRequestedIntegrationEventConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

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

    private static DomainChannel MapChannel(NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => DomainChannel.Email,
            NotificationChannel.Sms => DomainChannel.Sms,
            NotificationChannel.InApp => DomainChannel.InApp,
            _ => DomainChannel.Email
        };
}


