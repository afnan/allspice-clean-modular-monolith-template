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

    public IReadOnlySet<string> Permissions => _resolved ??= Resolve();

    public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);

    private IReadOnlySet<string> Resolve()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var roles = user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
        if (roles.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        // GetAwaiter().GetResult() is safe here: the cache hit path is synchronous, and on miss the load is a
        // short, scoped DB read. Resolution happens once per request (memoized in _resolved).
        var map = _cache.GetAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (map.RoleToPermissions.TryGetValue(role, out var perms))
            {
                result.UnionWith(perms);
            }
        }

        return result;
    }
}
