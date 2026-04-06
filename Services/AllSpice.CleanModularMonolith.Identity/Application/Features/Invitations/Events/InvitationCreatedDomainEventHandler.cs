using AllSpice.CleanModularMonolith.Identity.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Events;

public sealed class InvitationCreatedDomainEventHandler : IDomainEventHandler<InvitationCreatedDomainEvent>
{
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<InvitationCreatedDomainEventHandler> _logger;

    public InvitationCreatedDomainEventHandler(
        IIntegrationEventPublisher eventPublisher,
        ILogger<InvitationCreatedDomainEventHandler> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async ValueTask Handle(InvitationCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling InvitationCreatedDomainEvent for {Email}", notification.Email);

        var integrationEvent = new NotificationRequestedIntegrationEvent(
            EventId: Guid.NewGuid(),
            SourceModule: "Identity",
            RecipientUserId: string.Empty,
            RecipientEmail: notification.Email,
            RecipientPhoneNumber: null,
            Channel: NotificationChannel.Email,
            Subject: "You've been invited!",
            Body: $"Hello {notification.FirstName}, you have been invited to join the platform. " +
                  $"Your temporary password is: {notification.TempPassword}. " +
                  $"This invitation expires on {notification.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC.",
            TemplateKey: "invitation-created",
            ScheduledSendUtc: null,
            CorrelationId: notification.InvitationId.ToString(),
            Metadata: new Dictionary<string, string>
            {
                ["InvitationId"] = notification.InvitationId.ToString(),
                ["Role"] = notification.Role,
                ["Token"] = notification.Token.ToString()
            });

        await _eventPublisher.PublishAsync(integrationEvent, cancellationToken);
    }
}
