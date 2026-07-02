using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Mappers;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler(IUserRepository userRepository) : IRequestHandler<ListUsersQuery, Result<PaginationResult<UserDto>>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async ValueTask<Result<PaginationResult<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _userRepository.ListActivePagedAsync(request.Page, request.PageSize, cancellationToken);

        IReadOnlyCollection<UserDto> dtos = users
            .Select(UserMapper.ToDto)
            .ToList();

        return Result<PaginationResult<UserDto>>.Success(
            new PaginationResult<UserDto>(dtos, totalCount, request.Page, request.PageSize));
    }
}
