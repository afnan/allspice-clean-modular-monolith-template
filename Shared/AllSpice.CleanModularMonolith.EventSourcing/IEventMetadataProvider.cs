namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Supplies per-request metadata stamped on every appended event: the correlation id (ties the event to the
/// HTTP request/log scope) and the client's <c>Idempotency-Key</c> (lets a replayed command be recognised at
/// the stream level). The gateway implements it from <c>HttpContext</c>; background flows fall back to
/// <see cref="NullEventMetadataProvider"/>.
/// </summary>
public interface IEventMetadataProvider
{
    string? CorrelationId { get; }

    string? IdempotencyKey { get; }
}

/// <summary>Header keys used in event metadata. Centralised so readers and writers cannot drift.</summary>
public static class EventHeaders
{
    public const string IdempotencyKey = "idempotency-key";
}
