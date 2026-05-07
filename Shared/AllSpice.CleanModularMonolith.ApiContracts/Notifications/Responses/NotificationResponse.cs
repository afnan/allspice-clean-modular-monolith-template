namespace AllSpice.CleanModularMonolith.ApiContracts.Notifications.Responses;

/// <summary>
/// Public response shape for a queued or dispatched notification.
/// </summary>
public sealed record NotificationResponse(
    Guid Id,
    string Channel,
    string Subject,
    string Body,
    string RecipientUserId,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ScheduledSendUtc,
    DateTimeOffset? LastUpdatedUtc,
    string? CorrelationId,
    string Status);
