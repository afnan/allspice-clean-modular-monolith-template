namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests;

/// <summary>Deterministic clock for handler tests (ADR-0006: domain time always comes from TimeProvider).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
