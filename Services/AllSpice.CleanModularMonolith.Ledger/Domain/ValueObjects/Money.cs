using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;
using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;

/// <summary>An amount in a single currency. Immutable; arithmetic is currency-checked.</summary>
public sealed class Money : ValueObject
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsPositive => Amount > 0;

    public static Money Of(decimal amount, Currency currency)
    {
        Guard.Against.Null(currency);
        Guard.Against.Negative(amount);
        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, Guard.Against.Null(currency));

    public Money Add(Money other) => new(Amount + SameCurrency(other).Amount, Currency);

    public Money Subtract(Money other) => new(Amount - SameCurrency(other).Amount, Currency);

    private Money SameCurrency(Money other)
    {
        Guard.Against.Null(other);
        if (other.Currency != Currency)
        {
            throw new BusinessRuleViolationException($"Currency mismatch: {Currency.Code} vs {other.Currency.Code}.");
        }

        return other;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency.Code;
    }

    public override string ToString() => $"{Amount:0.00} {Currency.Code}";
}
