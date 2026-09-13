namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Request body for a deposit or withdrawal. The account id comes from the route.</summary>
public sealed class MoneyMovementRequest
{
    public decimal Amount { get; set; }

    /// <summary>Free-text reference recorded on the event (invoice number, payout id, …). Not personal data.</summary>
    public string Reference { get; set; } = string.Empty;
}
