namespace AllSpice.CleanModularMonolith.Identity.Application.DTOs;

/// <summary>
/// A single page of results plus its pagination metadata, carried as the value of a plain
/// <c>Ardalis.Result&lt;PagedList&lt;T&gt;&gt;</c>. (Ardalis' own <c>PagedResult&lt;T&gt;</c> is deliberately
/// avoided as a mediator response type: it derives from <c>Result&lt;T&gt;</c> but cannot be built in an
/// error state, so the domain-exception pipeline cannot map validation failures onto it — turning 400s
/// into 500s. A normal <c>Result&lt;PagedList&lt;T&gt;&gt;</c> maps like every other query.)
/// </summary>
public sealed record PagedList<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
