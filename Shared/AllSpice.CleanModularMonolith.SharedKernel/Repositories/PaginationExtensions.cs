using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

/// <summary>
/// Reusable paging helper so every list feature returns a consistent <see cref="PaginationResult{T}"/>
/// (items + total + page metadata) instead of hand-rolling the count/list/assemble dance per handler.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Executes a paged query and returns the page plus the unpaged total. The <paramref name="pagedSpecification"/>
    /// should carry the filter, ordering, and paging (use <see cref="SpecificationBuilderExtensions.ApplyPaging{T}"/>).
    /// <c>CountAsync</c> evaluates the spec's filter criteria but ignores its paging (Ardalis behavior), so the
    /// same spec yields both the page and the total — no second spec needed.
    /// </summary>
    public static async Task<PaginationResult<T>> ToPaginationResultAsync<T>(
        this IReadRepositoryBase<T> repository,
        ISpecification<T> pagedSpecification,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(pagedSpecification);
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var totalCount = await repository.CountAsync(pagedSpecification, cancellationToken);
        var items = await repository.ListAsync(pagedSpecification, cancellationToken);
        return new PaginationResult<T>(items, totalCount, pageNumber, pageSize);
    }
}
