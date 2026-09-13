using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.Accounts;

public class FundsDepositedUpcastsTests
{
    [Fact]
    public void FromV1_carries_all_fields_and_fills_the_missing_reference()
    {
        var when = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var v1 = new FundsDepositedV1(Guid.Parse("22222222-2222-2222-2222-222222222222"), 12.5m, "AUD", when);

        FundsDeposited upcast = FundsDepositedUpcasts.FromV1(v1);

        Assert.Equal(v1.AccountId, upcast.AccountId);
        Assert.Equal(12.5m, upcast.Amount);
        Assert.Equal("AUD", upcast.Currency);
        Assert.Equal(when, upcast.OccurredOnUtc);
        Assert.Equal(FundsDepositedUpcasts.LegacyReference, upcast.Reference);
    }
}
