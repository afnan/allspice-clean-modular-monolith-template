using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public interface IPermissionMapCache
{
    ValueTask<PermissionMap> GetAsync(CancellationToken cancellationToken);
    void Invalidate();
}

/// <summary>Caches the whole role→permission map in-process with a short TTL backstop. The map is small
/// (roles × permissions), so one cached copy per replica is fine. Plan B adds version-checked + Redis
/// pub-sub eviction for near-instant propagation.</summary>
public sealed class PermissionMapCache(IServiceScopeFactory scopeFactory, IMemoryCache cache) : IPermissionMapCache
{
    private const string CacheKey = "authz:map";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public void Invalidate() => _cache.Remove(CacheKey);

    public async ValueTask<PermissionMap> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out PermissionMap? cached) && cached is not null)
            return cached;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(CacheKey, out cached) && cached is not null)
                return cached;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IPermissionMapStore>();
            var map = await store.GetMapAsync(cancellationToken);
            _cache.Set(CacheKey, map, Ttl);
            return map;
        }
        finally { _lock.Release(); }
    }
}
