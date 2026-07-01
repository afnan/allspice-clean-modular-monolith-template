using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using StackExchange.Redis;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Publishes a best-effort nudge; ALL subscribers (including this node) evict. Failure is swallowed —
/// the 60s TTL backstop guarantees eventual convergence.</summary>
public sealed class RedisAuthzCacheInvalidator(IConnectionMultiplexer redis) : IAuthzCacheInvalidator
{
    private readonly IConnectionMultiplexer _redis = redis;

    public const string Channel = "authz:invalidate";

    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), "1");
        }
        catch
        {
            // best-effort: TTL backstop covers a lost nudge
        }
    }
}
