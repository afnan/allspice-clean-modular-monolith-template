using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// Multi-replica safety: the dispatcher claims a row by marking it <c>Dispatched</c>, guarded by the
/// <c>LastUpdatedUtc</c> optimistic-concurrency token. When two replicas grab the same row, exactly one wins
/// the claim and the other gets <see cref="DbUpdateConcurrencyException"/> (which the dispatcher handles by
/// skipping) — so a row can never be dispatched by two replicas.
/// </summary>
public sealed class NotificationClaimConcurrencyTests
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
    public async Task Concurrent_claims_of_the_same_row_let_exactly_one_replica_win()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();

        var notification = QueuedEmail();
        var id = notification.Id;
        await new NotificationRepository(database.Context).AddAsync(notification, CancellationToken.None);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        // Two replicas each load the same Pending row (before either claims it).
        await using var replicaA = database.NewContext();
        await using var replicaB = database.NewContext();

        var rowForA = await replicaA.Set<Notification>().SingleAsync(n => n.Id == id);
        var rowForB = await replicaB.Set<Notification>().SingleAsync(n => n.Id == id);

        // Replica A claims it first.
        rowForA.MarkDispatched();
        await replicaA.SaveChangesAsync(CancellationToken.None);

        // Replica B's claim must lose the optimistic-concurrency race.
        rowForB.MarkDispatched();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => replicaB.SaveChangesAsync(CancellationToken.None));

        // The row ends up Dispatched exactly once (A's claim), not double-attempted.
        await using var verify = database.NewContext();
        var persisted = await verify.Set<Notification>().SingleAsync(n => n.Id == id);
        Assert.Equal(NotificationStatus.Dispatched, persisted.Status);
    }
}
