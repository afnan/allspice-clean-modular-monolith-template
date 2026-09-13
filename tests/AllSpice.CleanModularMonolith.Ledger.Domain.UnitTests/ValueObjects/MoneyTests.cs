using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Equality_is_by_amount_and_currency()
    {
        Assert.Equal(Money.Of(10, Currency.Aud), Money.Of(10, Currency.Aud));
        Assert.NotEqual(Money.Of(10, Currency.Aud), Money.Of(10, Currency.Usd));
    }

    [Fact]
    public void Add_and_Subtract_require_same_currency()
    {
        var sum = Money.Of(10, Currency.Aud).Add(Money.Of(2.5m, Currency.Aud));
        Assert.Equal(12.5m, sum.Amount);

        Assert.Throws<BusinessRuleViolationException>(() => Money.Of(1, Currency.Aud).Add(Money.Of(1, Currency.Eur)));
    }

    [Fact]
    public void Of_rejects_negative_amounts()
    {
        Assert.ThrowsAny<ArgumentException>(() => Money.Of(-1, Currency.Aud));
    }

    [Theory]
    [InlineData("aud", true)]
    [InlineData("USD", true)]
    [InlineData("XXX", false)]
    public void Currency_TryFromCode_is_case_insensitive(string code, bool expected)
    {
        Assert.Equal(expected, Currency.TryFromCode(code, out _));
    }
}
