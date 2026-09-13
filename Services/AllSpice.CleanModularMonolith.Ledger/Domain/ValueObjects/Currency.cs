using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;

/// <summary>Closed set of supported ledger currencies (ISO 4217 numeric value, alpha code).</summary>
public sealed class Currency : SmartEnum<Currency>
{
    public static readonly Currency Aud = new(nameof(Aud), 36, "AUD");
    public static readonly Currency Usd = new(nameof(Usd), 840, "USD");
    public static readonly Currency Eur = new(nameof(Eur), 978, "EUR");

    private Currency(string name, int value, string code)
        : base(name, value)
    {
        Code = code;
    }

    /// <summary>ISO 4217 alpha code — what events store, so the stream never depends on this type's layout.</summary>
    public string Code { get; }

    public static bool TryFromCode(string? code, out Currency currency)
    {
        currency = List.FirstOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return currency is not null;
    }

    public static Currency FromCode(string code) =>
        TryFromCode(code, out var currency)
            ? currency
            : throw new BusinessRuleViolationException($"Unsupported currency '{code}'.");
}
