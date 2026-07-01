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
using Microsoft.Extensions.Options;
using Moq;
using Wolverine.EntityFrameworkCore;

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

    /// <summary>
    /// Regression guard for the opted-out infinite loop: when a user has opted out of a channel,
    /// the dispatcher must cancel the notification (not leave it Pending) so it is not re-selected
    /// by <c>DueNotificationsSpecification</c> on the next cycle.
    /// </summary>
    [Fact]
    public async Task DispatchPendingAsync_cancels_notification_when_user_has_opted_out()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationRepository(database.Context);
        var preferenceRepository = new NotificationPreferenceRepository(database.Context);

        var userId = Guid.NewGuid();
        var channel = NotificationChannel.Email;

        // Seed a notification for a user who has opted out of the Email channel.
        var notification = Notification.Queue(
            channel,
            NotificationRecipient.Create(userId.ToString(), "user@example.com", null),
            "Subject",
            "Body",
            templateKey: null,
            metadataJson: null,
            nowUtc: DateTimeOffset.UtcNow,
            scheduledSendUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            correlationId: "corr-optout");
        await repository.AddAsync(notification, CancellationToken.None);

        // Seed the opt-out preference (IsEnabled = false).
        var preference = NotificationPreference.Create(userId, channel, isEnabled: false, DateTimeOffset.UtcNow);
        await preferenceRepository.AddAsync(preference, CancellationToken.None);

        await database.Context.SaveChangesAsync(CancellationToken.None);

        var contentBuilder = new Mock<INotificationContentBuilder>();
        var outbox = new Mock<IDbContextOutbox>();
        var dispatcher = new NotificationDispatcher(
            repository,
            database.Context,
            [],
            preferenceRepository,
            contentBuilder.Object,
            outbox.Object,
            Microsoft.Extensions.Options.Options.Create(new NotificationDispatcherOptions()),
            TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

        var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        // The opted-out notification is NOT counted as delivered.
        Assert.Equal(0, processed);

        // Force a reload from the database — proves Cancel was persisted, not just mutated in memory.
        // If still Pending, DueNotificationsSpecification would re-select it every cycle (the bug).
        database.Context.ChangeTracker.Clear();
        var reloaded = await database.Context.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Cancelled, reloaded.Status);

        // The content builder and outbox must not have been touched — no delivery attempt was made.
        contentBuilder.Verify(b => b.BuildAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        outbox.Verify(o => o.PublishAsync(It.IsAny<object>(), It.IsAny<Wolverine.DeliveryOptions?>()), Times.Never);
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
            nowUtc: DateTimeOffset.UtcNow,
            scheduledSendUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            correlationId: "corr-1");
        await repository.AddAsync(notification, CancellationToken.None);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        var contentBuilder = new Mock<INotificationContentBuilder>();
        contentBuilder
            .Setup(b => b.BuildAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NotificationContent>.Success(new NotificationContent("Subject", "Body", false)));

        var outbox = new Mock<IDbContextOutbox>();
        var dispatcher = new NotificationDispatcher(
            repository,
            database.Context,
            [new AlwaysSucceedsEmailChannel()],
            preferenceRepository,
            contentBuilder.Object,
            outbox.Object,
            Microsoft.Extensions.Options.Options.Create(new NotificationDispatcherOptions()),
            TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

        var processed = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(1, processed);

        // F3: the delivered event is routed through the co-located outbox, not fire-and-forget.
        outbox.Verify(o => o.PublishAsync(
            It.IsAny<AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging.NotificationDeliveredIntegrationEvent>(),
            It.IsAny<Wolverine.DeliveryOptions?>()), Times.Once);

        // Force a reload from the database — proves the status change was actually persisted, not just
        // mutated in memory (the regression left it unsaved → still Pending on reload).
        database.Context.ChangeTracker.Clear();
        var reloaded = await database.Context.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Delivered, reloaded.Status);
    }
}
