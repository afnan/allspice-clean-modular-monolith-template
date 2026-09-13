namespace AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;

/// <summary>One audit-trail entry. <see cref="Data"/> is the stored event payload, serialised as-is.</summary>
public sealed record AccountHistoryEntryResponse(
    long Version, string EventType, DateTimeOffset Timestamp, string? CorrelationId, string? IdempotencyKey, object Data);
