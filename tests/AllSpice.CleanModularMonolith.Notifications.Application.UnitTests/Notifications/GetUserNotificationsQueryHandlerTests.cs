using Ardalis.Result;
using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.DTOs;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Queries.GetUserNotifications;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests.Notifications;

public class GetUserNotificationsQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _repositoryMock = new();
    private readonly GetUserNotificationsQueryHandler _handler;

    public GetUserNotificationsQueryHandlerTests()
    {
        _repositoryMock
            .Setup(repository => repository.ListAsync(It.IsAny<ISpecification<Notification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        _handler = new GetUserNotificationsQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsMappedDtoList()
    {
        var userId = "user-123";
        var notification = Notification.Queue(
            NotificationChannel.Email,
            NotificationRecipient.Create(userId, "user@example.com", null),
            "Subject",
            "Body",
            null,
            null);
        notification.ClearDomainEvents();
        var notifications = new List<Notification> { notification };

        _repositoryMock
            .Setup(repository => repository.ListAsync(It.IsAny<ISpecification<Notification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var result = await _handler.Handle(new GetUserNotificationsQuery(userId), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        var dto = Assert.Single(result.Value);
        Assert.Equal(notification.Id, dto.Id);
        Assert.Equal(notification.Channel.Name, dto.Channel);
        Assert.Equal(notification.Recipient.Email, dto.RecipientEmail);
    }

    [Fact]
    public async Task Handle_PassesSpecificationToRepository()
    {
        var capturedSpecification = default(ISpecification<Notification>);
        _repositoryMock
            .Setup(repository => repository.ListAsync(It.IsAny<ISpecification<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback((ISpecification<Notification> specification, CancellationToken _) => capturedSpecification = specification)
            .ReturnsAsync(new List<Notification>());

        await _handler.Handle(new GetUserNotificationsQuery("user-123"), CancellationToken.None);

        Assert.NotNull(capturedSpecification);
    }
}


