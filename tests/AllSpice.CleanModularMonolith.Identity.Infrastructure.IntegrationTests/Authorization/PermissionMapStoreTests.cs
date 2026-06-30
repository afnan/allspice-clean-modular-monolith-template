using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class PermissionMapStoreTests
{
    [Fact]
    public async Task GetMapAsync_projects_role_key_to_its_permission_keys()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync(); // mirrors Notifications TestDbContextFactory
        var role = Role.Create("platform-admin", null);
        var perm = Permission.Create("authz.manage", "Manage authz", isSystem: true);
        ctx.Roles.Add(role);
        ctx.Permissions.Add(perm);
        ctx.RolePermissions.Add(RolePermission.Create(role.Id, perm.Id));
        ctx.AuthzMapVersions.Add(AuthzMapVersion.Initial());
        await ctx.SaveChangesAsync();

        var store = new PermissionMapStore(ctx);
        var map = await store.GetMapAsync(default);

        Assert.True(map.RoleToPermissions.TryGetValue("platform-admin", out var perms));
        Assert.Contains("authz.manage", perms!);
    }
}
