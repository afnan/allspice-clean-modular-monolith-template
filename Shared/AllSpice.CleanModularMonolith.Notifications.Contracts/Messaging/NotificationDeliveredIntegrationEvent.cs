namespace AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;

public sealed record NotificationDeliveredIntegrationEvent(
    Guid EventId,
    Guid NotificationId,
    string Channel,
    string RecipientUserId,
    string? CorrelationId,
    int AttemptCount,
    DateTimeOffset DeliveredAtUtc);


