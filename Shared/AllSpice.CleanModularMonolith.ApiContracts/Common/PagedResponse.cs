namespace AllSpice.CleanModularMonolith.ApiContracts.Common;

/// <summary>
/// Envelope for a single page of results plus the pagination metadata a client needs to
/// navigate (total count and page count), so list endpoints don't silently drop the total.
/// </summary>
/// <typeparam name="T">The item type carried in <see cref="Items"/>.</typeparam>
public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
