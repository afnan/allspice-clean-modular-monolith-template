using Ardalis.GuardClauses;
using System.Text.RegularExpressions;

namespace AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

public sealed class EmailAddress : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmailAddress Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value);

        if (!EmailRegex.IsMatch(value))
        {
            throw new ArgumentException("Invalid email address format", nameof(value));
        }

        return new EmailAddress(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}


