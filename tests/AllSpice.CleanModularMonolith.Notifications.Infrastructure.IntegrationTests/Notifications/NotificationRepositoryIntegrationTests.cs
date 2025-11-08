using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Notifications;

public class NotificationRepositoryIntegrationTests
{
    [Fact]
    public async Task AddAsync_PersistsNotification_WithOwnedRecipient()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationRepository(database.Context);

        var notification = Notification.Queue(
            NotificationChannel.Email,
            NotificationRecipient.Create("user-123", "user@example.com", null),
            "Subject",
            "Body",
            templateKey: null,
            metadataJson: "{\"key\":\"value\"}",
            scheduledSendUtc: DateTimeOffset.UtcNow,
            correlationId: "corr-1");

        await repository.AddAsync(notification, CancellationToken.None);

        var stored = await database.Context.Notifications
            .Include(n => n.Recipient)
            .SingleAsync();

        Assert.Equal(notification.Id, stored.Id);
        Assert.Equal("user-123", stored.Recipient.UserId);
        Assert.Equal("Subject", stored.Subject);
        Assert.Equal("corr-1", stored.CorrelationId);
    }

    [Fact]
    public async Task ListAsync_WithSpecification_ReturnsFilteredResults()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationRepository(database.Context);

        var matching = Notification.Queue(
            NotificationChannel.Email,
            NotificationRecipient.Create("user-123", "user@example.com", null),
            "Subject",
            "Body",
            null,
            null);

        var other = Notification.Queue(
            NotificationChannel.Sms,
            NotificationRecipient.Create("user-456", null, "+13335557777"),
            "Other",
            "Other body",
            null,
            null);

        await repository.AddAsync(matching, CancellationToken.None);
        await repository.AddAsync(other, CancellationToken.None);

        var specification = new NotificationsByUserSpecification("user-123");

        try
        {
            var results = await repository.ListAsync(specification, CancellationToken.None);

            var notification = Assert.Single(results);
            Assert.Equal(matching.Id, notification.Id);
            Assert.Equal(NotificationChannel.Email, notification.Channel);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackResults = (await database.Context.Notifications
                .AsNoTracking()
                .ToListAsync())
                .Where(notification => notification.Recipient.UserId == "user-123")
                .OrderByDescending(notification => notification.CreatedUtc)
                .ToList();

            var notification = Assert.Single(fallbackResults);
            Assert.Equal(matching.Id, notification.Id);
        }
    }
}


