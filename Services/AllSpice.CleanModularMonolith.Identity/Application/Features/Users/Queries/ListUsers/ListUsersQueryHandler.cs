using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Mappers;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, PagedResult<IReadOnlyCollection<UserDto>>>
{
    private readonly IUserRepository _userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async ValueTask<PagedResult<IReadOnlyCollection<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _userRepository.ListActivePagedAsync(request.Page, request.PageSize, cancellationToken);

        IReadOnlyCollection<UserDto> dtos = users
            .Select(UserMapper.ToDto)
            .ToList();

        var totalPages = request.PageSize <= 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var pagedInfo = new PagedInfo(request.Page, request.PageSize, totalPages, totalCount);

        return new PagedResult<IReadOnlyCollection<UserDto>>(pagedInfo, dtos);
    }
}
