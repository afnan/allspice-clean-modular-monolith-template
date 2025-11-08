using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Events;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests.Notifications;

public class NotificationQueuedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_WritesDebugLogWithNotificationDetails()
    {
        var loggerMock = new Mock<ILogger<NotificationQueuedDomainEventHandler>>();
        var handler = new NotificationQueuedDomainEventHandler(loggerMock.Object);

        var notification = Notification.Queue(
            NotificationChannel.Email,
            NotificationRecipient.Create("user-123", "user@example.com", null),
            "Subject",
            "Body",
            null,
            null);
        notification.ClearDomainEvents();

        var domainEvent = new NotificationQueuedDomainEvent(notification);

        await handler.Handle(domainEvent, CancellationToken.None);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(notification.Id.ToString(), StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}


