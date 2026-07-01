using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListPermissions;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class ListPermissionsQueryHandlerTests
{
    [Fact]
    public async Task Lists_permissions_ordered_by_key()
    {
        var permB = Permission.Create("authz.manage", "Manage authz", isSystem: true);
        var permA = Permission.Create("authz.read", "Read authz", isSystem: true);

        var repo = new Mock<IPermissionRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([permB, permA]); // returned out-of-order; handler must sort

        var handler = new ListPermissionsQueryHandler(repo.Object);
        var result = await handler.Handle(new ListPermissionsQuery(), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("authz.manage", result.Value[0].Key);
        Assert.Equal("authz.read", result.Value[1].Key);
        Assert.True(result.Value[0].IsSystem);
    }
}
