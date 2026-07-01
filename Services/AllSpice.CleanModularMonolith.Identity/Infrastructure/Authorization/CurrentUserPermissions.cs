using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class CurrentUserPermissions(IPermissionMapCache cache, IHttpContextAccessor httpContextAccessor)
    : ICurrentUserPermissions
{
    private readonly IPermissionMapCache _cache = cache;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private IReadOnlySet<string>? _resolved;

    public async ValueTask<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
        => (await GetPermissionsAsync(cancellationToken)).Contains(permissionKey);

    public async ValueTask<IReadOnlySet<string>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        // Resolved once per request, then memoized (a request is logically single-threaded, so no locking).
        if (_resolved is not null)
        {
            return _resolved;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var roles = user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
        if (roles.Length == 0)
        {
            return _resolved = new HashSet<string>(StringComparer.Ordinal);
        }

        var map = await _cache.GetAsync(cancellationToken);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (map.RoleToPermissions.TryGetValue(role, out var perms))
            {
                result.UnionWith(perms);
            }
        }

        return _resolved = result;
    }
}
