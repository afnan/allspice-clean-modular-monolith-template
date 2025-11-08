using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

public static class SpecificationBuilderExtensions
{
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

        return builder.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }
}


