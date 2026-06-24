using AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Notifications;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests;

/// <summary>
/// Guards the unknown-channel-is-a-400 behavior: the request validator must reject an unrecognized channel
/// name so the endpoint never calls <c>NotificationChannel.FromName</c> on it (which would 500).
/// </summary>
public class QueueNotificationRequestValidatorTests
{
    private readonly QueueNotificationRequestValidator _validator = new();

    private static QueueNotificationRequest Request(string channel) => new()
    {
        Channel = channel,
        RecipientEmail = "user@example.com",
        Subject = "Subject",
        Body = "Body"
    };

    [Theory]
    [InlineData("Email")]
    [InlineData("sms")]   // case-insensitive
    [InlineData("InApp")]
    public void Accepts_known_channels(string channel)
    {
        var result = _validator.Validate(Request(channel));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(QueueNotificationRequest.Channel));
    }

    [Theory]
    [InlineData("CarrierPigeon")]
    [InlineData("")]
    public void Rejects_unknown_or_empty_channel(string channel)
    {
        var result = _validator.Validate(Request(channel));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(QueueNotificationRequest.Channel));
    }
}
