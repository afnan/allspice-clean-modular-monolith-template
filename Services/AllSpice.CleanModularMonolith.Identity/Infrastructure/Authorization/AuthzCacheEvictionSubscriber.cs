using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>
/// Subscribes to the authz-cache invalidation channel and evicts this replica's in-memory
/// permission map on each message.
/// </summary>
/// <remarks>
/// Subscribe failures (e.g. Redis unreachable at boot) are caught and logged as warnings; the
/// hosted service continues running so the rest of the application is not blocked. The 60-second
/// TTL on <see cref="PermissionMapCache"/> acts as a backstop: stale data ages out even without
/// a push eviction.
/// </remarks>
public sealed class AuthzCacheEvictionSubscriber(
    IConnectionMultiplexer redis,
    IPermissionMapCache cache,
    ILogger<AuthzCacheEvictionSubscriber> logger) : IHostedService
{
    private readonly IConnectionMultiplexer _redis = redis;
    private readonly IPermissionMapCache _cache = cache;
    private readonly ILogger<AuthzCacheEvictionSubscriber> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetSubscriber().SubscribeAsync(
                RedisChannel.Literal(RedisAuthzCacheInvalidator.Channel), (_, _) => _cache.Invalidate());
        }
        catch (Exception ex)
        {
            // Redis is unreachable at startup — log a warning and continue. The 60-second TTL on
            // PermissionMapCache is the backstop; eviction will resume if Redis reconnects later.
            _logger.LogWarning(ex,
                "Could not subscribe to authz-cache eviction channel at startup; falling back to TTL-based expiry.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetSubscriber().UnsubscribeAsync(
                RedisChannel.Literal(RedisAuthzCacheInvalidator.Channel));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unsubscribe from authz-cache eviction channel during shutdown.");
        }
    }
}
