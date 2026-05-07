using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.Users;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.GetUser;

public sealed class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly IReadRepository<User> _users;

    public GetUserQueryHandler(IReadRepository<User> users)
    {
        _users = users;
    }

    public async ValueTask<Result<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.FirstOrDefaultAsync(new UserByExternalIdSpec(request.ExternalId), cancellationToken);

        if (user is null)
        {
            return Result<UserDto>.NotFound($"User with external ID '{request.ExternalId}' not found.");
        }

        return Result<UserDto>.Success(UserDto.From(user));
    }
}
