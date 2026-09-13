namespace AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;

/// <summary>Response returned when a ledger account is opened successfully.</summary>
public sealed record OpenAccountResponse(Guid AccountId);
