namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>Default when no request context exists (jobs, tests): stamps nothing.</summary>
public sealed class NullEventMetadataProvider : IEventMetadataProvider
{
    public string? CorrelationId => null;

    public string? IdempotencyKey => null;
}
