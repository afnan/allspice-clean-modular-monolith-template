using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class CurrentUserPermissionsTests
{
    private static (CurrentUserPermissions sut, Mock<IPermissionMapCache> cache) Build(string[] roles, PermissionMap map)
    {
        var cache = new Mock<IPermissionMapCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(map);

        var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test", ClaimTypes.Name, ClaimTypes.Role);
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });

        return (new CurrentUserPermissions(cache.Object, accessor.Object), cache);
    }

    private static PermissionMap MapWith(string role, params string[] perms)
        => new(1, new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [role] = perms.ToHashSet(StringComparer.Ordinal),
        });

    [Fact]
    public async Task Grants_permission_from_role()
        => Assert.True(await Build(["platform-admin"], MapWith("platform-admin", "authz.read")).sut.HasPermissionAsync("authz.read"));

    [Fact]
    public async Task Denies_unmapped_permission()
        => Assert.False(await Build(["platform-admin"], MapWith("platform-admin", "authz.read")).sut.HasPermissionAsync("authz.manage"));

    [Fact]
    public async Task Empty_roles_deny_all()
        => Assert.False(await Build([], MapWith("platform-admin", "authz.read")).sut.HasPermissionAsync("authz.read"));

    [Fact]
    public async Task Role_match_is_case_insensitive()
        => Assert.True(await Build(["Platform-Admin"], MapWith("platform-admin", "authz.read")).sut.HasPermissionAsync("authz.read"));

    [Fact]
    public async Task Resolves_once_and_memoizes()
    {
        var (sut, cache) = Build(["platform-admin"], MapWith("platform-admin", "authz.read"));

        await sut.HasPermissionAsync("authz.read");
        await sut.HasPermissionAsync("authz.read");

        cache.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
