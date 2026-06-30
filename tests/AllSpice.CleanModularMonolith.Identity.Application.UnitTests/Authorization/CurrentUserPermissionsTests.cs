using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class CurrentUserPermissionsTests
{
    private static CurrentUserPermissions Build(string[] roles, PermissionMap map)
    {
        var cache = new Mock<IPermissionMapCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(map);

        var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test", ClaimTypes.Name, ClaimTypes.Role);
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });

        return new CurrentUserPermissions(cache.Object, accessor.Object);
    }

    private static PermissionMap MapWith(string role, params string[] perms)
        => new(1, new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [role] = perms.ToHashSet(StringComparer.Ordinal),
        });

    [Fact]
    public void Grants_permission_from_role()
        => Assert.True(Build(["platform-admin"], MapWith("platform-admin", "authz.read")).HasPermission("authz.read"));

    [Fact]
    public void Denies_unmapped_permission()
        => Assert.False(Build(["platform-admin"], MapWith("platform-admin", "authz.read")).HasPermission("authz.manage"));

    [Fact]
    public void Empty_roles_deny_all()
        => Assert.False(Build([], MapWith("platform-admin", "authz.read")).HasPermission("authz.read"));

    [Fact]
    public void Role_match_is_case_insensitive()
        => Assert.True(Build(["Platform-Admin"], MapWith("platform-admin", "authz.read")).HasPermission("authz.read"));
}
