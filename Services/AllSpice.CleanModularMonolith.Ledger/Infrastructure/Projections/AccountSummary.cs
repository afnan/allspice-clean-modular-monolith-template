namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;

/// <summary>
/// Inline single-stream projection document: the account's current state for reads. <see cref="Version"/> is
/// the stream version the document reflects (an "as of" marker for clients and for concurrency-aware UIs).
/// </summary>
public sealed class AccountSummary
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset LastActivityUtc { get; set; }
}
