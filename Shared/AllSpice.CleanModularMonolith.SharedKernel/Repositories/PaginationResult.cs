namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

public sealed record PaginationResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    // Override the auto-generated positional properties to validate at construction time.
    // Guards TotalPages from integer.MinValue when PageSize == 0 and PageNumber from an invalid state.
    // ToPaginationResultAsync enforces the same constraints, but a direct constructor call bypasses that.
    public int PageSize { get; init; } = PageSize >= 1
        ? PageSize
        : throw new ArgumentOutOfRangeException(nameof(PageSize), PageSize, "PageSize must be at least 1.");

    public int PageNumber { get; init; } = PageNumber >= 1
        ? PageNumber
        : throw new ArgumentOutOfRangeException(nameof(PageNumber), PageNumber, "PageNumber must be at least 1.");

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}


