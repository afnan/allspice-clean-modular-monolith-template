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

        var notificationId = Guid.NewGuid();
        var domainEvent = new NotificationQueuedDomainEvent(notificationId, "user-123", "Email");

        await handler.Handle(domainEvent, CancellationToken.None);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(notificationId.ToString(), StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}


