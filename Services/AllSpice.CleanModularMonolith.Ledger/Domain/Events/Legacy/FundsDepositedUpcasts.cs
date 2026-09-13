namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;

/// <summary>Pure transformation from legacy event shapes to the current one. Registered with the store in Infrastructure.</summary>
public static class FundsDepositedUpcasts
{
    /// <summary>Reference used for deposits recorded before references were captured.</summary>
    public const string LegacyReference = "legacy-import";

    public static FundsDeposited FromV1(FundsDepositedV1 v1) =>
        new(v1.AccountId, v1.Amount, v1.Currency, LegacyReference, v1.OccurredOnUtc);
}
