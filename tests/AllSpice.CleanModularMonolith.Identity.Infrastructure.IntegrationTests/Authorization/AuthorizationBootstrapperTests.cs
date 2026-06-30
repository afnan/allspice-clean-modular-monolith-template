using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AuthorizationOptions = AllSpice.CleanModularMonolith.Identity.Infrastructure.Options.AuthorizationOptions;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class AuthorizationBootstrapperTests
{
    /// <summary>
    /// Verifies that BootstrapAsync grants both authz permissions to the configured role,
    /// and that running it twice produces exactly two RolePermission rows (idempotent).
    /// The Permission rows for authz.read / authz.manage are seeded first (via the
    /// reconciler) so the FK constraints on authz_role_permissions are satisfied.
    /// </summary>
    [Fact]
    public async Task Grants_authz_perms_to_configured_role_idempotently()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();

        // Arrange – seed the two system permissions the bootstrapper will map to.
        // This mirrors what the reconciler does at startup, satisfying the FK constraint
        // on authz_role_permissions before any RolePermission row is inserted.
        ctx.Permissions.Add(Permission.Create(Permissions.AuthzRead, "System permission authz.read", isSystem: true));
        ctx.Permissions.Add(Permission.Create(Permissions.AuthzManage, "System permission authz.manage", isSystem: true));
        ctx.AuthzMapVersions.Add(AuthzMapVersion.Initial());
        await ctx.SaveChangesAsync();

        var options = new OptionsWrapper<AuthorizationOptions>(new AuthorizationOptions { BootstrapAdminRole = "platform-admin" });
        var bootstrapper = BuildBootstrapper(ctx, options);

        // Act – run twice to prove idempotency.
        await bootstrapper.BootstrapAsync(default);
        await ctx.SaveChangesAsync();

        await bootstrapper.BootstrapAsync(default);
        await ctx.SaveChangesAsync();

        // Assert – exactly one role row with the right key.
        var role = await ctx.Roles.SingleAsync(r => r.Key == "platform-admin");
        Assert.NotNull(role);

        // Assert – exactly 2 RolePermission rows, one per authz permission.
        var mappings = await ctx.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync();
        Assert.Equal(2, mappings.Count);

        // Assert – one mapping per expected permission.
        var permRead = await ctx.Permissions.SingleAsync(p => p.Key == Permissions.AuthzRead);
        var permManage = await ctx.Permissions.SingleAsync(p => p.Key == Permissions.AuthzManage);
        Assert.Contains(mappings, m => m.PermissionId == permRead.Id);
        Assert.Contains(mappings, m => m.PermissionId == permManage.Id);
    }

    /// <summary>
    /// Verifies that BootstrapAsync is a no-op when BootstrapAdminRole is null or empty,
    /// leaving the Roles table empty.
    /// </summary>
    [Fact]
    public async Task No_op_when_unset()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();

        var options = new OptionsWrapper<AuthorizationOptions>(new AuthorizationOptions { BootstrapAdminRole = null });
        var bootstrapper = BuildBootstrapper(ctx, options);

        await bootstrapper.BootstrapAsync(default);
        await ctx.SaveChangesAsync();

        var roleCount = await ctx.Roles.CountAsync();
        Assert.Equal(0, roleCount);
    }

    private static AuthorizationBootstrapper BuildBootstrapper(
        Persistence.IdentityDbContext ctx,
        IOptions<AuthorizationOptions> options)
        => new(
            options,
            new RoleRepository(ctx),
            new PermissionRepository(ctx),
            new RolePermissionRepository(ctx),
            new AuthzMapVersionRepository(ctx),
            NullLogger<AuthorizationBootstrapper>.Instance);
}
