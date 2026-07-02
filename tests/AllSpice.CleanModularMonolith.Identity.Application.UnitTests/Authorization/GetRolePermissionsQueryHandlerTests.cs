using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.GetRolePermissions;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class GetRolePermissionsQueryHandlerTests
{
    [Fact]
    public async Task Returns_NotFound_when_role_does_not_exist()
    {
        var roleRepo = new Mock<IRoleRepository>();
        roleRepo.Setup(r => r.GetByKeyAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var rolePermRepo = new Mock<IRolePermissionRepository>();
        var permRepo = new Mock<IPermissionRepository>();

        var handler = new GetRolePermissionsQueryHandler(roleRepo.Object, rolePermRepo.Object, permRepo.Object);
        var result = await handler.Handle(new GetRolePermissionsQuery("nonexistent"), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        rolePermRepo.Verify(r => r.ListByRoleIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_permission_keys_for_existing_role()
    {
        var role = Role.Create("admin", "Admin role");
        var perm = Permission.Create("authz.read", "Read authz", isSystem: true);
        var mapping = RolePermission.Create(role.Id, perm.Id);

        var roleRepo = new Mock<IRoleRepository>();
        roleRepo.Setup(r => r.GetByKeyAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var rolePermRepo = new Mock<IRolePermissionRepository>();
        rolePermRepo.Setup(r => r.ListByRoleIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mapping]);

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.ListByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([perm]);

        var handler = new GetRolePermissionsQueryHandler(roleRepo.Object, rolePermRepo.Object, permRepo.Object);
        var result = await handler.Handle(new GetRolePermissionsQuery("admin"), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Single(result.Value);
        Assert.Equal("authz.read", result.Value[0]);
    }
}
