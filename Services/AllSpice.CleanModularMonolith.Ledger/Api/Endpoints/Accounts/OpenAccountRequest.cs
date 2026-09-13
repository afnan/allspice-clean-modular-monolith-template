namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Request body for opening a ledger account.</summary>
public sealed class OpenAccountRequest
{
    /// <summary>Local user UUID (User.Id) that owns the account.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>ISO 4217 alpha code: AUD, USD or EUR.</summary>
    public string Currency { get; set; } = "AUD";
}
