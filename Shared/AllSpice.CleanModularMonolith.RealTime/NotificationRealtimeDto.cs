namespace AllSpice.CleanModularMonolith.RealTime;

public sealed record NotificationRealtimeDto(
    Guid NotificationId,
    string Subject,
    string Body,
    bool IsHtml,
    DateTimeOffset CreatedUtc,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);


