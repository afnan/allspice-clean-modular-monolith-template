using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.Users;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, Result<IReadOnlyCollection<UserDto>>>
{
    private readonly IReadRepository<User> _users;

    public ListUsersQueryHandler(IReadRepository<User> users)
    {
        _users = users;
    }

    public async ValueTask<Result<IReadOnlyCollection<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _users.ListAsync(new ActiveUsersPagedSpec(request.Page, request.PageSize), cancellationToken);

        var dtos = users
            .Select(UserDto.From)
            .ToList();

        return Result<IReadOnlyCollection<UserDto>>.Success(dtos);
    }
}
