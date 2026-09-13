namespace AllSpice.CleanModularMonolith.Ledger.Application.DTOs;

public sealed record AccountSummaryDto(
    Guid AccountId,
    Guid OwnerUserId,
    string Currency,
    decimal Balance,
    string Status,
    long Version,
    DateTimeOffset LastActivityUtc);
