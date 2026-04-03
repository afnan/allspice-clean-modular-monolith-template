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
    IReadOnlyDictionary<string, string>? Metadata)
{
    // Parameterless constructor for Wolverine deserialization
    public NotificationRequestedIntegrationEvent()
        : this(Guid.Empty, string.Empty, string.Empty, null, null, default, string.Empty, string.Empty, null, null, null, null) { }
}
