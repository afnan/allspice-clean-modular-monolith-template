using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// One persisted event with its store metadata — what an audit/history read returns. <see cref="Version"/> is
/// the position within the stream; <see cref="Sequence"/> is the store-wide sequence number.
/// </summary>
public sealed record StoredEvent(
    long Version,
    long Sequence,
    DateTimeOffset Timestamp,
    string EventType,
    string? CorrelationId,
    IReadOnlyDictionary<string, object?> Headers,
    IDomainEvent Data);
