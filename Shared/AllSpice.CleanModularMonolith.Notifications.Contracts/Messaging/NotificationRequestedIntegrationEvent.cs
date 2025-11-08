namespace AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;

public sealed record NotificationRequestedIntegrationEvent(
    Guid EventId,
    string SourceModule,
    string RecipientUserId,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    NotificationChannel Channel,
    string Subject,
    string Body,
    string? TemplateKey,
    DateTimeOffset? ScheduledSendUtc,
    string? CorrelationId,
    IReadOnlyDictionary<string, string>? Metadata);


