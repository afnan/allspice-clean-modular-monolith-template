using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Subscribes to the invalidation channel and evicts this replica's in-memory map on each message.</summary>
public sealed class AuthzCacheEvictionSubscriber(IConnectionMultiplexer redis, IPermissionMapCache cache) : IHostedService
{
    private readonly IConnectionMultiplexer _redis = redis;
    private readonly IPermissionMapCache _cache = cache;

    public async Task StartAsync(CancellationToken cancellationToken)
        => await _redis.GetSubscriber().SubscribeAsync(
            RedisChannel.Literal(RedisAuthzCacheInvalidator.Channel), (_, _) => _cache.Invalidate());

    public async Task StopAsync(CancellationToken cancellationToken)
        => await _redis.GetSubscriber().UnsubscribeAsync(RedisChannel.Literal(RedisAuthzCacheInvalidator.Channel));
}
