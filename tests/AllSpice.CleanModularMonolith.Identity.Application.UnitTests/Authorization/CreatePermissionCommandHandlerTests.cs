using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.CreatePermission;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class CreatePermissionCommandHandlerTests
{
    [Fact]
    public async Task Returns_Conflict_when_key_already_exists()
    {
        var existing = Permission.Create("authz.manage", "Manage authz", isSystem: false);

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.GetByKeyAsync("authz.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var versionRepo = new Mock<IAuthzMapVersionRepository>();
        var cacheInvalidator = new Mock<IAuthzCacheInvalidator>();

        var handler = new CreatePermissionCommandHandler(permRepo.Object, versionRepo.Object, cacheInvalidator.Object);

        var result = await handler.Handle(
            new CreatePermissionCommand("authz.manage", "Duplicate key"), CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        permRepo.Verify(r => r.AddAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()), Times.Never);
        versionRepo.Verify(r => r.GetTrackedAsync(It.IsAny<CancellationToken>()), Times.Never);
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Creates_permission_bumps_version_and_invalidates()
    {
        var version = AuthzMapVersion.Initial();

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.GetByKeyAsync("cms.write", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission?)null);
        permRepo.Setup(r => r.AddAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission p, CancellationToken _) => p);

        var versionRepo = new Mock<IAuthzMapVersionRepository>();
        versionRepo.Setup(r => r.GetTrackedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var cacheInvalidator = new Mock<IAuthzCacheInvalidator>();
        cacheInvalidator.Setup(r => r.InvalidateAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreatePermissionCommandHandler(permRepo.Object, versionRepo.Object, cacheInvalidator.Object);

        var result = await handler.Handle(
            new CreatePermissionCommand("cms.write", "Write CMS content"), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(1, version.Version);
        permRepo.Verify(r => r.AddAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()), Times.Once);
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
