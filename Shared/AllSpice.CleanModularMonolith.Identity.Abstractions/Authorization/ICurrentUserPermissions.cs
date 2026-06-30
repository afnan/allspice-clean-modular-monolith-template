namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>The current request's resolved permission set. Scoped; resolved lazily on first call from the
/// authenticated principal's role claims (memoized for the request), so proxied routes that never check a
/// permission pay nothing. Async so the cache-miss DB load never blocks a thread.</summary>
public interface ICurrentUserPermissions
{
    ValueTask<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlySet<string>> GetPermissionsAsync(CancellationToken cancellationToken = default);
}
