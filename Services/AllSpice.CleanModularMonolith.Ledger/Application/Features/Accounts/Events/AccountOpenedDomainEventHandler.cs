using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Events;

/// <summary>
/// Stream events ARE domain events, so this handler runs inside the OpenAccount transaction (drain loop) and
/// the integration event it publishes enrols the same outbox transaction: the account, its projection and
/// the notification request commit or roll back together. Reuses the Notifications contract — no new
/// Contracts project is needed to talk to an existing module.
/// </summary>
public sealed class AccountOpenedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<AccountOpened>
{
    private readonly IIntegrationEventPublisher _publisher = publisher;

    public async ValueTask Handle(AccountOpened notification, CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(new NotificationRequestedIntegrationEvent(
            EventId: Guid.NewGuid(),
            SourceModule: "Ledger",
            RecipientUserId: notification.OwnerUserId.ToString(),
            RecipientEmail: null,
            RecipientPhoneNumber: null,
            Channel: NotificationChannel.InApp,
            Subject: "Ledger account opened",
            Body: $"Your {notification.Currency} ledger account is ready.",
            TemplateKey: null,
            ScheduledSendUtc: null,
            CorrelationId: null,
            Metadata: new Dictionary<string, string> { ["accountId"] = notification.AccountId.ToString() }), cancellationToken);
    }
}
