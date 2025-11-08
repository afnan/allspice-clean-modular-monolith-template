namespace AllSpice.CleanModularMonolith.Notifications.Application.DTOs;

public sealed record NotificationDto(
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


