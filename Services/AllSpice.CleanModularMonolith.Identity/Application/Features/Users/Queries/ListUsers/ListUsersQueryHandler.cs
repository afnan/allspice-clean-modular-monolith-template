using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Mappers;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler(IUserRepository userRepository) : IRequestHandler<ListUsersQuery, Result<PagedList<UserDto>>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async ValueTask<Result<PagedList<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _userRepository.ListActivePagedAsync(request.Page, request.PageSize, cancellationToken);

        IReadOnlyCollection<UserDto> dtos = users
            .Select(UserMapper.ToDto)
            .ToList();

        var totalPages = request.PageSize <= 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return Result<PagedList<UserDto>>.Success(
            new PagedList<UserDto>(dtos, request.Page, request.PageSize, totalCount, totalPages));
    }
}
