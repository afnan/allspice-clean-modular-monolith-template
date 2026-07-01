using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

public static class SpecificationBuilderExtensions
{
    /// <summary>Upper bound on page size so a caller-supplied value can't allocate an unbounded result set
    /// or overflow the OFFSET computation. Requests above this are clamped.</summary>
    public const int MaxPageSize = 200;

    public static ISpecificationBuilder<T> ApplyPaging<T>(this ISpecificationBuilder<T> builder, int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        pageSize = Math.Min(pageSize, MaxPageSize);

        // Compute the offset in 64-bit so an absurd page number can't overflow int32 and wrap to a
        // negative Skip (which the database provider would reject).
        var skip = (long)(pageNumber - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Requested page is out of range.");
        }

        return builder.Skip((int)skip).Take(pageSize);
    }
}


