using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionMapCacheInvalidationTests
{
    private sealed class CountingStore : IPermissionMapStore
    {
        public int CallCount { get; private set; }

        public Task<PermissionMap> GetMapAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new PermissionMap(1, new Dictionary<string, IReadOnlySet<string>>()));
        }
    }

    [Fact]
    public async Task Invalidate_forces_reload_on_next_get()
    {
        // arrange: a PermissionMapCache over a scope factory whose IPermissionMapStore counts GetMapAsync calls
        var store = new CountingStore();
        var services = new ServiceCollection();
        services.AddSingleton<IPermissionMapStore>(store);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new PermissionMapCache(scopeFactory, memoryCache);

        // act: GetAsync (loads, count=1) -> GetAsync (cached, count=1) -> Invalidate() -> GetAsync (reloads, count=2)
        await cache.GetAsync(CancellationToken.None);
        Assert.Equal(1, store.CallCount);

        await cache.GetAsync(CancellationToken.None);
        Assert.Equal(1, store.CallCount);

        cache.Invalidate();
        await cache.GetAsync(CancellationToken.None);

        // assert: store.CallCount == 2
        Assert.Equal(2, store.CallCount);
    }
}
