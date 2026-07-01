using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests.Preferences;

public class UpsertNotificationPreferenceCommandHandlerTests
{
    private static readonly Guid SampleUserId = new("22222222-2222-2222-2222-222222222222");

    private readonly Mock<INotificationPreferenceRepository> _repositoryMock = new();
    private readonly UpsertNotificationPreferenceCommandHandler _handler;

    public UpsertNotificationPreferenceCommandHandlerTests()
    {
        _repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference preference, CancellationToken _) => preference);
        _repositoryMock
            .Setup(repository => repository.UpdateAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(1));

        _handler = new UpsertNotificationPreferenceCommandHandler(_repositoryMock.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_AddsPreference_WhenNoneExists()
    {
        _repositoryMock
            .Setup(repository => repository.GetByUserAndChannelAsync(It.IsAny<Guid>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        var command = new UpsertNotificationPreferenceCommand(SampleUserId, NotificationChannel.Email, true);

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
        var existing = NotificationPreference.Create(SampleUserId, NotificationChannel.Email, true, DateTimeOffset.UtcNow);
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
    public async Task Handle_PropagatesException_WhenRepositoryThrows()
    {
        // The handler no longer swallows exceptions into a client-facing Result.Error — that leaked raw
        // exception text and turned transient DB faults into permanent 400s. Faults must propagate so the
        // pipeline behaviors classify them (consistent with QueueNotificationCommandHandler).
        _repositoryMock
            .Setup(repository => repository.GetByUserAndChannelAsync(It.IsAny<Guid>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var command = new UpsertNotificationPreferenceCommand(SampleUserId, NotificationChannel.Email, true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None).AsTask());
    }
}
