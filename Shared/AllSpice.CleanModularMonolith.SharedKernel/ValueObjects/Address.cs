using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

public sealed class Address : ValueObject
{
    private Address(string line1, string? line2, string city, string state, string postalCode, string country)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public string Line1 { get; }

    public string? Line2 { get; }

    public string City { get; }

    public string State { get; }

    public string PostalCode { get; }

    public string Country { get; }

    public static Address Create(
        string line1,
        string? line2,
        string city,
        string state,
        string postalCode,
        string country)
    {
        Guard.Against.NullOrWhiteSpace(line1);
        Guard.Against.NullOrWhiteSpace(city);
        Guard.Against.NullOrWhiteSpace(state);
        Guard.Against.NullOrWhiteSpace(postalCode);
        Guard.Against.NullOrWhiteSpace(country);

        return new Address(line1.Trim(), line2?.Trim(), city.Trim(), state.Trim(), postalCode.Trim(), country.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() => string.Join(", ", new[] { Line1, Line2, City, State, PostalCode, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}


