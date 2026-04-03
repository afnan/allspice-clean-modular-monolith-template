using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, Result<IReadOnlyCollection<UserDto>>>
{
    private readonly IUserRepository _userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async ValueTask<Result<IReadOnlyCollection<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListActiveAsync(cancellationToken);

        var dtos = users
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(UserDto.From)
            .ToList();

        return Result<IReadOnlyCollection<UserDto>>.Success(dtos);
    }
}
