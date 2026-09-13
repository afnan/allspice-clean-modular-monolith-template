namespace AllSpice.CleanModularMonolith.Ledger.Application.DTOs;

/// <summary>One audit-trail entry: stream position, stored type name, store timestamp, request metadata, payload.</summary>
public sealed record AccountHistoryEntryDto(
    long Version,
    string EventType,
    DateTimeOffset Timestamp,
    string? CorrelationId,
    string? IdempotencyKey,
    object Data);
