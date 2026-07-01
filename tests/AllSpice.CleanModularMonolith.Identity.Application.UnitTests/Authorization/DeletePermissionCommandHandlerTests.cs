using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.DeletePermission;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class DeletePermissionCommandHandlerTests
{
    [Fact]
    public async Task Deleting_a_system_permission_is_forbidden()
    {
        var permission = Permission.Create("authz.read", "Read authz", isSystem: true);
        var id = permission.Id;

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.GetByIdAsync<Guid>(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        var versionRepo = new Mock<IAuthzMapVersionRepository>();
        var cacheInvalidator = new Mock<IAuthzCacheInvalidator>();

        var handler = new DeletePermissionCommandHandler(permRepo.Object, versionRepo.Object, cacheInvalidator.Object, new PostCommitActions());
        var result = await handler.Handle(new DeletePermissionCommand(id), CancellationToken.None);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        versionRepo.Verify(r => r.GetTrackedAsync(It.IsAny<CancellationToken>()), Times.Never);
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deleting_a_custom_permission_succeeds_and_invalidates()
    {
        var permission = Permission.Create("cms.write", "Write CMS content", isSystem: false);
        var id = permission.Id;
        var version = AuthzMapVersion.Initial();

        var permRepo = new Mock<IPermissionRepository>();
        permRepo.Setup(r => r.GetByIdAsync<Guid>(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);
        permRepo.Setup(r => r.DeleteAsync(permission, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(0));

        var versionRepo = new Mock<IAuthzMapVersionRepository>();
        versionRepo.Setup(r => r.GetTrackedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var cacheInvalidator = new Mock<IAuthzCacheInvalidator>();
        cacheInvalidator.Setup(r => r.InvalidateAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var postCommit = new PostCommitActions();
        var handler = new DeletePermissionCommandHandler(permRepo.Object, versionRepo.Object, cacheInvalidator.Object, postCommit);
        var result = await handler.Handle(new DeletePermissionCommand(id), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(1, version.Version); // bumped from 0 to 1
        permRepo.Verify(r => r.DeleteAsync(permission, It.IsAny<CancellationToken>()), Times.Once);

        // Eviction is deferred to a post-commit action, not fired during Handle.
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        foreach (var action in postCommit.Drain())
        {
            await action(CancellationToken.None);
        }
        cacheInvalidator.Verify(r => r.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
