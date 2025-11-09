using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

namespace AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;

public sealed class ExternalUserId : ValueObject
{
    private ExternalUserId()
    {
        Value = string.Empty;
    }

    private ExternalUserId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ExternalUserId From(string value)
    {
        Guard.Against.NullOrWhiteSpace(value);
        return new ExternalUserId(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}


