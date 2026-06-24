using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Features.Users;

public sealed class ListUsersQueryHandlerTests
{
    [Fact]
    public async Task Handle_surfaces_total_count_and_page_metadata()
    {
        // The repository knows there are 137 active users; we ask for page 2 of 20.
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(r => r.ListActivePagedAsync(2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)Array.Empty<User>(), 137));

        var handler = new ListUsersQueryHandler(repository.Object);

        var result = await handler.Handle(new ListUsersQuery(2, 20), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(137, result.Value.TotalCount);
        Assert.Equal(7, result.Value.TotalPages); // ceil(137 / 20)
        Assert.Empty(result.Value.Items);
    }
}
