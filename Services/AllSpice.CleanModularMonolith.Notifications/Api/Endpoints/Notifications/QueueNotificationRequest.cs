using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Notifications;

/// <summary>
/// Client request payload for queuing notifications.
/// </summary>
public sealed class QueueNotificationRequest
{
    /// <summary>Identifier of the target user inside the notifications domain.</summary>
    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>Optional email address for email notifications.</summary>
    public string? RecipientEmail { get; set; }

    /// <summary>Optional phone number for SMS notifications.</summary>
    public string? RecipientPhoneNumber { get; set; }

    /// <summary>Notification channel name (e.g. Email, Sms, InApp).</summary>
    public string Channel { get; set; } = NotificationChannel.Email.Name;

    /// <summary>Notification subject or title.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Notification body rendered with optional template tokens.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional template key to use when generating the notification.</summary>
    public string? TemplateKey { get; set; }

    /// <summary>Arbitrary metadata that accompanies the notification.</summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Optional scheduled send time in UTC.</summary>
    public DateTimeOffset? ScheduledSendUtc { get; set; }

    /// <summary>Correlation identifier supplied by the caller.</summary>
    public string? CorrelationId { get; set; }
}
