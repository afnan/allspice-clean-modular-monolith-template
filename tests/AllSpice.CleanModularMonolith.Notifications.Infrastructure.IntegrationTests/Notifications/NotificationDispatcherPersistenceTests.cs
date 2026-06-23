using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// Regression guard for the unit-of-work change: the dispatcher runs in a BackgroundService scope (not an
/// ITransactional Mediator command), so with track-only repositories it must flush its own writes. Before the
/// fix, `UpdateAsync` no-opped and the notification stayed `Pending` forever — an infinite duplicate-send loop.
/// </summary>
public sealed class NotificationDispatcherPersistenceTests
{
    private sealed class AlwaysSucceedsEmailChannel : INotificationChannel
    {
        public NotificationChannel Channel => NotificationChannel.Email;
        public Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    [Fact]
    public async Task DispatchPendingAsync_persists_the_delivered_status()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationRepository(database.Context);
        var preferenceRepository = new NotificationPreferenceRepository(database.Context);

        // Userless recipient so no preference lookup; scheduled in the past so it is "due".
        var notification = Notification.Queue(
            NotificationChannel.Email,
            NotificationRecipient.Create(string.Empty, "user@example.com", null),
            "Subject",
            "Body",
            templateKey: null,
            metadataJson: null,
            scheduledSendUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            correlationId: "corr-1");
        await repository.AddAsync(notification, CancellationToken.None);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        var contentBuilder = new Mock<INotificationContentBuilder>();
        contentBuilder
            .Setup(b => b.BuildAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NotificationContent>.Success(new NotificationContent("Subject", "Body", false)));

        var dispatcher = new NotificationDispatcher(
            repository,
            database.Context,
            [new AlwaysSucceedsEmailChannel()],
            preferenceRepository,
            contentBuilder.Object,
            Mock.Of<IMessageBus>(),
            NullLogger<NotificationDispatcher>.Instance);

        var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(1, processed);

        // Force a reload from the database — proves the status change was actually persisted, not just
        // mutated in memory (the regression left it unsaved → still Pending on reload).
        database.Context.ChangeTracker.Clear();
        var reloaded = await database.Context.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Delivered, reloaded.Status);
    }
}
