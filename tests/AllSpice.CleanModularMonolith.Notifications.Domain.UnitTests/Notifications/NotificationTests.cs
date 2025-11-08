using System.Reflection;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Notifications;

public class NotificationTests
{
    private static NotificationRecipient CreateRecipient() =>
        NotificationRecipient.Create("user-123", "user@example.com", null);

    [Fact]
    public void Queue_RaisesQueuedDomainEvent()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null);

        var domainEvent = Assert.Single(notification.DomainEvents);
        Assert.IsType<NotificationQueuedDomainEvent>(domainEvent);
        Assert.Equal(notification, ((NotificationQueuedDomainEvent)domainEvent).Notification);
    }

    [Fact]
    public void Queue_AllowsEmptySubjectAndBody_WhenTemplateProvided()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            string.Empty,
            string.Empty,
            "welcome-email",
            null);

        Assert.Equal("welcome-email", notification.TemplateKey);
        Assert.Equal(string.Empty, notification.Subject);
        Assert.Equal(string.Empty, notification.Body);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-1)]
    [InlineData(0)]
    public void IsReadyToDispatch_ReturnsTrue_WhenDue(int minutesOffset)
    {
        var scheduledSend = DateTimeOffset.UtcNow.AddMinutes(minutesOffset);
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null,
            scheduledSend);

        Assert.True(notification.IsReadyToDispatch(DateTimeOffset.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public void IsReadyToDispatch_ReturnsFalse_WhenStatusNotPending()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null);

        notification.MarkDispatched();

        Assert.False(notification.IsReadyToDispatch(DateTimeOffset.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public void RecordAttempt_IncrementsAttemptCountAndTimestamps()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null);

        notification.RecordAttempt();

        Assert.Equal(1, notification.AttemptCount);
        Assert.NotNull(notification.LastAttemptedUtc);
        Assert.Equal(notification.LastAttemptedUtc, notification.LastUpdatedUtc);
    }

    [Fact]
    public void HandleFailure_SetsFailedStatus_WhenMaxAttemptsReached()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null);

        var maxAttemptsField = typeof(Notification)
            .GetField("MaxDeliveryAttempts", BindingFlags.NonPublic | BindingFlags.Static);
        var maxAttempts = Assert.IsType<int>(maxAttemptsField?.GetValue(null));

        for (var i = 0; i < maxAttempts; i++)
        {
            notification.RecordAttempt();
        }

        notification.HandleFailure("fatal");

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Null(notification.NextAttemptUtc);
        Assert.Equal("fatal", notification.LastError);
    }

    [Fact]
    public void HandleFailure_SchedulesRetry_WhenAttemptsRemain()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null);

        notification.RecordAttempt();
        var beforeFailure = DateTimeOffset.UtcNow;

        notification.HandleFailure("temporary");

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal("temporary", notification.LastError);
        Assert.NotNull(notification.NextAttemptUtc);
        Assert.True(notification.NextAttemptUtc > beforeFailure);
    }

    [Fact]
    public void MarkDelivered_ResetsErrorAndNextAttempt()
    {
        var notification = Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null);

        notification.RecordAttempt();
        notification.HandleFailure("temporary");

        notification.MarkDelivered();

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
        Assert.Null(notification.LastError);
        Assert.Null(notification.NextAttemptUtc);
    }
}


