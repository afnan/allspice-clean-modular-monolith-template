using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

public sealed class DateRange : ValueObject
{
    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public static DateRange Create(DateOnly start, DateOnly end)
    {
        Guard.Against.OutOfRange(end, nameof(end), start, DateOnly.MaxValue, "End date must be on or after start date.");
        return new DateRange(start, end);
    }

    public bool Overlaps(DateRange other)
        => Start <= other.End && End >= other.Start;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}


