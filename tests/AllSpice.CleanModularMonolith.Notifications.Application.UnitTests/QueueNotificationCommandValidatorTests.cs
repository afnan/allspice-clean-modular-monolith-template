using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests;

public class QueueNotificationCommandValidatorTests
{
    private readonly QueueNotificationCommandValidator _validator = new();

    private static QueueNotificationCommand Command(
        string recipientUserId = "",
        string? email = "user@example.com",
        string? phone = null) =>
        new(
            RecipientUserId: recipientUserId,
            RecipientEmail: email,
            RecipientPhoneNumber: phone,
            Channel: NotificationChannel.Email,
            Subject: "Subject",
            Body: "Body",
            TemplateKey: null,
            Metadata: null,
            ScheduledSendUtc: null,
            CorrelationId: null);

    [Fact]
    public void UserlessRecipient_WithEmail_IsValid()
    {
        // Regression: invitation/system emails publish with an empty RecipientUserId.
        var result = _validator.Validate(Command(recipientUserId: ""));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UserlessRecipient_WithPhoneOnly_IsValid()
    {
        var result = _validator.Validate(Command(recipientUserId: "", email: null, phone: "+61400000000"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidGuidUserId_IsValid()
    {
        var result = _validator.Validate(Command(recipientUserId: Guid.NewGuid().ToString()));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void NonGuidUserId_IsInvalid()
    {
        var result = _validator.Validate(Command(recipientUserId: "not-a-guid"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(QueueNotificationCommand.RecipientUserId));
    }

    [Fact]
    public void NoEmailAndNoPhone_IsInvalid()
    {
        var result = _validator.Validate(Command(email: null, phone: null));
        Assert.False(result.IsValid);
    }
}
