using System.Reflection;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Notifications;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static NotificationRecipient CreateRecipient() =>
        NotificationRecipient.Create("user-123", "user@example.com", null);

    private static Notification Queue(DateTimeOffset? scheduledSend = null) =>
        Notification.Queue(
            NotificationChannel.Email,
            CreateRecipient(),
            "Subject",
            "Body",
            null,
            null,
            Now,
            scheduledSend);

    [Fact]
    public void Queue_RaisesQueuedDomainEvent()
    {
        var notification = Queue();

        var domainEvent = Assert.Single(notification.DomainEvents);
        var queuedEvent = Assert.IsType<NotificationQueuedDomainEvent>(domainEvent);
        Assert.Equal(notification.Id, queuedEvent.NotificationId);
        Assert.Equal(Now, queuedEvent.OccurredOnUtc);
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
            null,
            Now);

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
        var scheduledSend = Now.AddMinutes(minutesOffset);
        var notification = Queue(scheduledSend);

        Assert.True(notification.IsReadyToDispatch(Now.AddMinutes(1)));
    }

    [Fact]
    public void IsReadyToDispatch_ReturnsFalse_WhenStatusNotPending()
    {
        var notification = Queue();

        notification.MarkDispatched(Now);

        Assert.False(notification.IsReadyToDispatch(Now.AddMinutes(1)));
    }

    [Fact]
    public void RecordAttempt_IncrementsAttemptCountAndTimestamps()
    {
        var notification = Queue();

        notification.RecordAttempt(Now);

        Assert.Equal(1, notification.AttemptCount);
        Assert.Equal(Now, notification.LastAttemptedUtc);
        Assert.Equal(notification.LastAttemptedUtc, notification.LastUpdatedUtc);
    }

    [Fact]
    public void HandleFailure_SetsFailedStatus_WhenMaxAttemptsReached()
    {
        var notification = Queue();

        var maxAttemptsField = typeof(Notification)
            .GetField("MaxDeliveryAttempts", BindingFlags.NonPublic | BindingFlags.Static);
        var maxAttempts = Assert.IsType<int>(maxAttemptsField?.GetValue(null));

        for (var i = 0; i < maxAttempts; i++)
        {
            notification.RecordAttempt(Now);
        }

        notification.HandleFailure("fatal", Now);

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Null(notification.NextAttemptUtc);
        Assert.Equal("fatal", notification.LastError);
    }

    [Fact]
    public void HandleFailure_SchedulesRetry_WhenAttemptsRemain()
    {
        var notification = Queue();

        notification.RecordAttempt(Now);

        notification.HandleFailure("temporary", Now);

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal("temporary", notification.LastError);
        Assert.NotNull(notification.NextAttemptUtc);
        Assert.True(notification.NextAttemptUtc > Now);
    }

    [Fact]
    public void MarkDelivered_ResetsErrorAndNextAttempt()
    {
        var notification = Queue();

        notification.RecordAttempt(Now);
        notification.HandleFailure("temporary", Now);

        notification.MarkDelivered(Now);

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
        Assert.Null(notification.LastError);
        Assert.Null(notification.NextAttemptUtc);
    }
}
