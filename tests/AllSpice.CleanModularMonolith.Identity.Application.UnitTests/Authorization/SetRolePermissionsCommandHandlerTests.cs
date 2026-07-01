using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.SetRolePermissions;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class SetRolePermissionsCommandHandlerTests
{
    [Fact]
    public async Task Replaces_mapping_bumps_version_and_invalidates()
    {
        var role = Role.Create("admin", "Admin role");
        var oldPerm = Permission.Create("authz.read", "Read authz", isSystem: true);
        var newPerm = Permission.Create("authz.manage", "Manage authz", isSystem: true);
        var oldMapping = RolePermission.Create(role.Id, oldPerm.Id);
        var version = AuthzMapVersion.Initial();

        var roleRepo = new Mock<IRoleRepository>();
        roleRepo.Setup(r => r.GetByKeyAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var rolePermRepo = new Mock<IRolePermissionRepository>();
        rolePermRepo.Setup(r => r.ListByRoleIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldMapping]);

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.GetByKeyAsync("authz.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newPerm);

        var versionRepo = new Mock<IAuthzMapVersionRepository>();
        versionRepo.Setup(r => r.GetTrackedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var cacheInvalidator = new Mock<IAuthzCacheInvalidator>();
        cacheInvalidator.Setup(r => r.InvalidateAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var postCommit = new PostCommitActions();
        var handler = new SetRolePermissionsCommandHandler(
            roleRepo.Object, rolePermRepo.Object, permRepo.Object, versionRepo.Object, cacheInvalidator.Object, postCommit);

        var result = await handler.Handle(
            new SetRolePermissionsCommand("admin", ["authz.manage"]), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(1, version.Version); // bumped from 0 to 1
        rolePermRepo.Verify(
            r => r.RemoveRange(It.Is<IEnumerable<RolePermission>>(x => x.Contains(oldMapping))),
            Times.Once);
        rolePermRepo.Verify(
            r => r.Add(It.Is<RolePermission>(rp => rp.RoleId == role.Id && rp.PermissionId == newPerm.Id)),
            Times.Once);
        // The handler defers eviction to a post-commit action — nothing is invalidated during Handle itself.
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);

        // Draining the queue (as TransactionBehavior does after commit) performs the eviction.
        foreach (var action in postCommit.Drain())
        {
            await action(CancellationToken.None);
        }

        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_NotFound_when_role_does_not_exist()
    {
        var roleRepo = new Mock<IRoleRepository>();
        roleRepo.Setup(r => r.GetByKeyAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var handler = new SetRolePermissionsCommandHandler(
            roleRepo.Object,
            new Mock<IRolePermissionRepository>().Object,
            new Mock<IPermissionRepository>().Object,
            new Mock<IAuthzMapVersionRepository>().Object,
            new Mock<IAuthzCacheInvalidator>().Object,
            new PostCommitActions());

        var result = await handler.Handle(
            new SetRolePermissionsCommand("nonexistent", ["authz.read"]), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Returns_Invalid_when_permission_key_is_unknown()
    {
        var role = Role.Create("admin", "Admin role");

        var roleRepo = new Mock<IRoleRepository>();
        roleRepo.Setup(r => r.GetByKeyAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var rolePermRepo = new Mock<IRolePermissionRepository>();

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.GetByKeyAsync("unknown.key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission?)null);

        var versionRepo = new Mock<IAuthzMapVersionRepository>();
        var cacheInvalidator = new Mock<IAuthzCacheInvalidator>();

        var handler = new SetRolePermissionsCommandHandler(
            roleRepo.Object,
            rolePermRepo.Object,
            permRepo.Object,
            versionRepo.Object,
            cacheInvalidator.Object,
            new PostCommitActions());

        var result = await handler.Handle(
            new SetRolePermissionsCommand("admin", ["unknown.key"]), CancellationToken.None);

        Assert.Equal(ResultStatus.Invalid, result.Status);

        // No mutations must have been staged — the two-pass design guarantees this.
        rolePermRepo.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<RolePermission>>()), Times.Never);
        rolePermRepo.Verify(r => r.Add(It.IsAny<RolePermission>()), Times.Never);
        versionRepo.Verify(r => r.GetTrackedAsync(It.IsAny<CancellationToken>()), Times.Never);
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
