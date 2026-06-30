using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class AuthorizationCatalogReconcilerTests
{
    [Fact]
    public async Task Seeds_catalog_keys_as_system_and_is_idempotent()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();
        var reconciler = new AuthorizationCatalogReconciler(
            ctx,
            [],
            NullLogger<AuthorizationCatalogReconciler>.Instance);

        await reconciler.ReconcileAsync(default);
        await reconciler.ReconcileAsync(default);

        var count = await ctx.Permissions.CountAsync(p => p.Key == "authz.manage");
        Assert.Equal(1, count);

        var perm = await ctx.Permissions.SingleAsync(p => p.Key == "authz.manage");
        Assert.True(perm.IsSystem);
    }

    [Fact]
    public async Task Leaves_orphan_db_permission_intact()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();
        ctx.Permissions.Add(Permission.Create("custom.thing", "x", isSystem: false));
        await ctx.SaveChangesAsync();

        var reconciler = new AuthorizationCatalogReconciler(
            ctx,
            [],
            NullLogger<AuthorizationCatalogReconciler>.Instance);

        await reconciler.ReconcileAsync(default);

        var still = await ctx.Permissions.SingleAsync(p => p.Key == "custom.thing");
        Assert.NotNull(still);
    }
}
