using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListRoles;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class ListRolesQueryHandlerTests
{
    [Fact]
    public async Task Lists_roles_ordered_by_key()
    {
        var roleB = Role.Create("platform-admin", "Platform administrator");
        var roleA = Role.Create("content-editor", "Content editor");

        var repo = new Mock<IRoleRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([roleB, roleA]); // returned out-of-order; handler must sort

        var handler = new ListRolesQueryHandler(repo.Object);
        var result = await handler.Handle(new ListRolesQuery(), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("content-editor", result.Value[0].Key);
        Assert.Equal("platform-admin", result.Value[1].Key);
    }
}
