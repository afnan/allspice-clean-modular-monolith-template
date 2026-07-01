using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests;

public class QueueNotificationCommandValidatorTests
{
    private readonly QueueNotificationCommandValidator _validator = new();

    private static QueueNotificationCommand Command(
        string recipientUserId = "",
        string? email = "user@example.com",
        string? phone = null,
        NotificationChannel? channel = null) =>
        new(
            RecipientUserId: recipientUserId,
            RecipientEmail: email,
            RecipientPhoneNumber: phone,
            Channel: channel ?? NotificationChannel.Email,
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

    [Fact]
    public void EmailChannel_WithoutEmail_IsInvalid()
    {
        // An Email-channel notification with no email address can never deliver; the channel-conditional rule
        // rejects it instead of letting it burn every delivery attempt.
        var result = _validator.Validate(Command(email: null, phone: "+61400000000"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(QueueNotificationCommand.RecipientEmail));
    }

    [Fact]
    public void EmailChannel_WithMalformedEmail_IsInvalid()
    {
        var result = _validator.Validate(Command(email: "not-an-email"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(QueueNotificationCommand.RecipientEmail));
    }

    [Fact]
    public void SmsChannel_IsRejected_UntilSupported()
    {
        // SMS has no channel handler yet: it must be rejected up-front rather than queued to fail repeatedly.
        var result = _validator.Validate(Command(email: null, phone: "+61400000000", channel: NotificationChannel.Sms));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(QueueNotificationCommand.Channel));
    }
}
