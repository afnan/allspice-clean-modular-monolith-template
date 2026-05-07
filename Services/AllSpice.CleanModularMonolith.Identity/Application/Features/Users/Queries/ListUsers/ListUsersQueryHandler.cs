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
        var (users, _) = await _userRepository.ListActivePagedAsync(request.Page, request.PageSize, cancellationToken);

        var dtos = users
            .Select(UserDto.From)
            .ToList();

        return Result<IReadOnlyCollection<UserDto>>.Success(dtos);
    }
}
