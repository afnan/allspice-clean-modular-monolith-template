namespace AllSpice.CleanModularMonolith.RealTime;

/// <summary>
/// Payload delivered to clients when a notification is broadcast in real time.
/// </summary>
/// <param name="NotificationId">Unique identifier for the notification.</param>
/// <param name="Subject">Subject or title of the notification.</param>
/// <param name="Body">Notification body content.</param>
/// <param name="IsHtml">Indicates whether the body is HTML.</param>
/// <param name="CreatedUtc">Creation timestamp (UTC).</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="Metadata">Additional metadata associated with the notification.</param>
public sealed record NotificationRealtimeDto(
    Guid NotificationId,
    string Subject,
    string Body,
    bool IsHtml,
    DateTimeOffset CreatedUtc,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);

