namespace AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;

/// <summary>Current state of a ledger account, as materialised by the inline projection.</summary>
public sealed record AccountSummaryResponse(
    Guid AccountId, Guid OwnerUserId, string Currency, decimal Balance, string Status, long Version, DateTimeOffset LastActivityUtc);
