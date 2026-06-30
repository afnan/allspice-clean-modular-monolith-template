namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Signals every replica to drop its cached role→permission map. Redis pub-sub when configured;
/// in-process eviction when Redis is absent (single node).</summary>
public interface IAuthzCacheInvalidator
{
    Task InvalidateAsync(CancellationToken cancellationToken);
}
