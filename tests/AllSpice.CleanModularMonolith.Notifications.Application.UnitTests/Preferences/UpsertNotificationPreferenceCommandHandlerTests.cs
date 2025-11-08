using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests.Preferences;

public class UpsertNotificationPreferenceCommandHandlerTests
{
    private readonly Mock<INotificationPreferenceRepository> _repositoryMock = new();
    private readonly UpsertNotificationPreferenceCommandHandler _handler;

    public UpsertNotificationPreferenceCommandHandlerTests()
    {
        _repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference preference, CancellationToken _) => preference);
        _repositoryMock
            .Setup(repository => repository.UpdateAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _handler = new UpsertNotificationPreferenceCommandHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_AddsPreference_WhenNoneExists()
    {
        _repositoryMock
            .Setup(repository => repository.GetByUserAndChannelAsync(It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        var command = new UpsertNotificationPreferenceCommand("user-123", NotificationChannel.Email, true);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        _repositoryMock.Verify(
            repository => repository.AddAsync(
                It.Is<NotificationPreference>(preference =>
                    preference.UserId == command.UserId &&
                    preference.Channel == command.Channel &&
                    preference.IsEnabled == command.IsEnabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UpdatesPreference_WhenExisting()
    {
        var existing = NotificationPreference.Create("user-123", NotificationChannel.Email, true);
        _repositoryMock
            .Setup(repository => repository.GetByUserAndChannelAsync(existing.UserId, existing.Channel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpsertNotificationPreferenceCommand(existing.UserId, existing.Channel, false);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.False(existing.IsEnabled);
        _repositoryMock.Verify(
            repository => repository.UpdateAsync(existing, It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenRepositoryThrows()
    {
        _repositoryMock
            .Setup(repository => repository.GetByUserAndChannelAsync(It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var command = new UpsertNotificationPreferenceCommand("user-123", NotificationChannel.Email, true);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Contains("database down", result.Errors.Single());
    }
}


