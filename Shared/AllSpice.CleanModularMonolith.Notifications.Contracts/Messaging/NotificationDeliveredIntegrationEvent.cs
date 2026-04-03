namespace AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;

public sealed record NotificationDeliveredIntegrationEvent(
    Guid EventId,
    Guid NotificationId,
    string Channel,
    string RecipientUserId,
    string? CorrelationId,
    int AttemptCount,
    DateTimeOffset DeliveredAtUtc)
{
    // Parameterless constructor for Wolverine deserialization
    public NotificationDeliveredIntegrationEvent()
        : this(Guid.Empty, Guid.Empty, string.Empty, string.Empty, null, 0, default) { }
}
