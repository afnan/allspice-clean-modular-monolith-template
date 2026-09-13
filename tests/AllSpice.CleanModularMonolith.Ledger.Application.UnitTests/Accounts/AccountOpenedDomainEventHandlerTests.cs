using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class AccountOpenedDomainEventHandlerTests
{
    [Fact]
    public async Task Publishes_an_in_app_notification_request_for_the_owner()
    {
        var publisher = new Mock<IIntegrationEventPublisher>();
        NotificationRequestedIntegrationEvent? published = null;
        publisher.Setup(p => p.PublishAsync(It.IsAny<NotificationRequestedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback((NotificationRequestedIntegrationEvent e, CancellationToken _) => published = e)
            .Returns(ValueTask.CompletedTask);
        var handler = new AccountOpenedDomainEventHandler(publisher.Object);
        var owner = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        await handler.Handle(new AccountOpened(accountId, owner, "AUD", DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal("Ledger", published.SourceModule);
        Assert.Equal(owner.ToString(), published.RecipientUserId);
        Assert.Equal(NotificationChannel.InApp, published.Channel);
        Assert.Equal(accountId.ToString(), published.Metadata!["accountId"]);
    }
}
