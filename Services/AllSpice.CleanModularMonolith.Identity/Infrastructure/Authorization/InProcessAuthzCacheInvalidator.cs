using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Used when Redis is not configured: evicts this process's cache directly (single-node correctness).</summary>
public sealed class InProcessAuthzCacheInvalidator(IPermissionMapCache cache) : IAuthzCacheInvalidator
{
    private readonly IPermissionMapCache _cache = cache;

    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        _cache.Invalidate();
        return Task.CompletedTask;
    }
}
