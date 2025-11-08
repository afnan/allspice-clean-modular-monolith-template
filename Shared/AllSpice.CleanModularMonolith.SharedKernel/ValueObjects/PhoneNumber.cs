using Ardalis.GuardClauses;
using System.Text.RegularExpressions;

namespace AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

public sealed class PhoneNumber : ValueObject
{
    private static readonly Regex PhoneRegex = new(
        @"^[+]*[(]{0,1}[0-9]{1,4}[)]{0,1}[-\s\./0-9]*$",
        RegexOptions.Compiled);

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PhoneNumber Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value);

        var trimmed = value.Trim();

        if (!PhoneRegex.IsMatch(trimmed))
        {
            throw new ArgumentException("Invalid phone number format", nameof(value));
        }

        return new PhoneNumber(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}


