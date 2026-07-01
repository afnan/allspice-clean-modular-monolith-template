using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.SetRolePermissions;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

/// <summary>
/// Integration tests for <see cref="SetRolePermissionsCommandHandler"/> using a real SQLite-backed
/// <see cref="IdentityDbContext"/> to verify that the diff-based Pass 2 correctly handles overlapping
/// permission sets without throwing a unique-constraint exception or churning unchanged rows.
/// </summary>
public sealed class SetRolePermissionsIntegrationTests
{
    /// <summary>
    /// Verifies the core overlap scenario: setting {b, c} when the existing set is {a, b} results in
    /// exactly {b, c} with NO unique-constraint exception. The overlapping row (b) is neither removed
    /// nor re-added — the diff leaves it untouched.
    /// </summary>
    [Fact]
    public async Task Overlap_set_produces_correct_mappings_without_constraint_exception()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();

        // Arrange — seed role, 3 permissions, AuthzMapVersion, and initial {a, b} mapping.
        var role = Role.Create("editor", "Editor role");
        var permA = Permission.Create("content.read", "Read content", isSystem: false);
        var permB = Permission.Create("content.write", "Write content", isSystem: false);
        var permC = Permission.Create("content.publish", "Publish content", isSystem: false);
        var version = AuthzMapVersion.Initial();

        ctx.Roles.Add(role);
        ctx.Permissions.AddRange(permA, permB, permC);
        ctx.AuthzMapVersions.Add(version);
        ctx.RolePermissions.Add(RolePermission.Create(role.Id, permA.Id));
        ctx.RolePermissions.Add(RolePermission.Create(role.Id, permB.Id));
        await ctx.SaveChangesAsync();

        var handler = BuildHandler(ctx);

        // Act — set {b, c}. The b row overlaps with the existing {a, b} set.
        // The diff must: remove a (not in desired), keep b (in both), add c (new).
        // NO unique-constraint exception must be thrown.
        var result = await handler.Handle(
            new SetRolePermissionsCommand("editor", ["content.write", "content.publish"]),
            CancellationToken.None);

        await ctx.SaveChangesAsync();

        // Assert — operation succeeded.
        Assert.Equal(Ardalis.Result.ResultStatus.Ok, result.Status);

        // Assert — mappings are exactly {b, c}.
        var mappings = await ctx.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync();
        Assert.Equal(2, mappings.Count);
        Assert.Contains(mappings, m => m.PermissionId == permB.Id);
        Assert.Contains(mappings, m => m.PermissionId == permC.Id);
        Assert.DoesNotContain(mappings, m => m.PermissionId == permA.Id);
    }

    /// <summary>
    /// Verifies that calling SetRolePermissions with the SAME set as already persisted is a true no-op:
    /// no exception, no row changes, and the version is NOT bumped (because the diff produces empty
    /// toRemove and toAdd lists).
    /// </summary>
    [Fact]
    public async Task No_op_set_does_not_throw_and_leaves_rows_unchanged()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();

        // Arrange — seed role, 2 permissions, AuthzMapVersion, and mapping {b, c}.
        var role = Role.Create("viewer", "Viewer role");
        var permB = Permission.Create("content.write", "Write content", isSystem: false);
        var permC = Permission.Create("content.publish", "Publish content", isSystem: false);
        var version = AuthzMapVersion.Initial();

        ctx.Roles.Add(role);
        ctx.Permissions.AddRange(permB, permC);
        ctx.AuthzMapVersions.Add(version);
        ctx.RolePermissions.Add(RolePermission.Create(role.Id, permB.Id));
        ctx.RolePermissions.Add(RolePermission.Create(role.Id, permC.Id));
        await ctx.SaveChangesAsync();

        var versionBefore = version.Version;
        var handler = BuildHandler(ctx);

        // Act — set {b, c} again (identical to existing).
        var result = await handler.Handle(
            new SetRolePermissionsCommand("viewer", ["content.write", "content.publish"]),
            CancellationToken.None);

        await ctx.SaveChangesAsync();

        // Assert — no exception, success result.
        Assert.Equal(Ardalis.Result.ResultStatus.Ok, result.Status);

        // Assert — rows unchanged: still exactly {b, c}.
        var mappings = await ctx.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync();
        Assert.Equal(2, mappings.Count);
        Assert.Contains(mappings, m => m.PermissionId == permB.Id);
        Assert.Contains(mappings, m => m.PermissionId == permC.Id);

        // Assert — version NOT bumped on a no-op.
        var versionAfter = (await ctx.AuthzMapVersions.FirstAsync()).Version;
        Assert.Equal(versionBefore, versionAfter);
    }

    private static SetRolePermissionsCommandHandler BuildHandler(
        AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.IdentityDbContext ctx)
        => new(
            new RoleRepository(ctx),
            new RolePermissionRepository(ctx),
            new PermissionRepository(ctx),
            new AuthzMapVersionRepository(ctx),
            new NoOpCacheInvalidator(),
            new AllSpice.CleanModularMonolith.SharedKernel.Behaviors.PostCommitActions());

    /// <summary>No-op invalidator: integration tests do not need Redis.</summary>
    private sealed class NoOpCacheInvalidator : IAuthzCacheInvalidator
    {
        public Task InvalidateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
