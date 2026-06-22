using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;

/// <summary>
/// Test double for <see cref="IDbContextFactory{TContext}"/> that creates context instances via a
/// delegate (e.g. one bound to a shared in-memory SQLite connection). Use when exercising code that
/// depends on <see cref="IDbContextFactory{TContext}"/> rather than a scoped DbContext.
/// </summary>
internal sealed class TestDbContextFactory<TContext> : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly Func<TContext> _createContext;

    public TestDbContextFactory(Func<TContext> createContext)
    {
        _createContext = createContext;
    }

    public TContext CreateDbContext() => _createContext();

    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_createContext());
}
