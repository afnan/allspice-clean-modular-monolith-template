using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests.Notifications;

public class QueueNotificationCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _repositoryMock = new();
    private readonly QueueNotificationCommandHandler _handler;

    public QueueNotificationCommandHandlerTests()
    {
        _repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification notification, CancellationToken _) => notification);

        _handler = new QueueNotificationCommandHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenNotificationQueued()
    {
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var command = new QueueNotificationCommand(
            "user-123",
            "user@example.com",
            null,
            NotificationChannel.Email,
            "Subject",
            "Body",
            null,
            metadata,
            DateTimeOffset.UtcNow.AddMinutes(5),
            "corr-1");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.NotEqual(Guid.Empty, result.Value);

        _repositoryMock.Verify(
            repository => repository.AddAsync(
                It.Is<Notification>(n =>
                    n.Recipient.UserId == command.RecipientUserId &&
                    n.Subject == command.Subject &&
                    n.Body == command.Body &&
                    n.CorrelationId == command.CorrelationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenRecipientInvalid()
    {
        var command = new QueueNotificationCommand(
            "user-123",
            null,
            null,
            NotificationChannel.Email,
            "Subject",
            "Body",
            null,
            null,
            null,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.NotEmpty(result.Errors);
        _repositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}


