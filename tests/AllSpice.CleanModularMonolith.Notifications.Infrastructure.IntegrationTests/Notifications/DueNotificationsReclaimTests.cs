using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// F4: a notification stranded in <c>Dispatched</c> (process crashed between mark-dispatched and send) must be
/// reclaimed once it's older than the reclaim cutoff, but a freshly-dispatched one must not. Tested by varying
/// the cutoff rather than backdating the row.
/// </summary>
public sealed class DueNotificationsReclaimTests
{
    private static Notification QueuedEmail() => Notification.Queue(
        NotificationChannel.Email,
        NotificationRecipient.Create(string.Empty, "user@example.com", null),
        "Subject",
        "Body",
        templateKey: null,
        metadataJson: null,
        scheduledSendUtc: DateTimeOffset.UtcNow.AddMinutes(-1));

    [Fact]
    public async Task Stranded_dispatched_notification_is_reclaimed_when_past_cutoff()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationRepository(database.Context);

        var notification = QueuedEmail();
        notification.MarkDispatched(); // LastUpdatedUtc ~= now; status Dispatched
        await repository.AddAsync(notification, CancellationToken.None);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;

        // reclaimBefore in the FUTURE => the dispatched row counts as stranded => selected.
        var reclaimed = await repository.ListAsync(
            new DueNotificationsSpecification(now, reclaimBefore: now.AddMinutes(5)), CancellationToken.None);
        Assert.Contains(reclaimed, n => n.Id == notification.Id);

        // reclaimBefore in the PAST => freshly dispatched, within timeout => NOT selected.
        var notReclaimed = await repository.ListAsync(
            new DueNotificationsSpecification(now, reclaimBefore: now.AddMinutes(-5)), CancellationToken.None);
        Assert.DoesNotContain(notReclaimed, n => n.Id == notification.Id);
    }

    [Fact]
    public async Task Pending_due_notification_is_always_selected()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationRepository(database.Context);

        var notification = QueuedEmail(); // stays Pending
        await repository.AddAsync(notification, CancellationToken.None);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var due = await repository.ListAsync(
            new DueNotificationsSpecification(now, reclaimBefore: now.AddMinutes(-5)), CancellationToken.None);

        Assert.Contains(due, n => n.Id == notification.Id);
    }
}
