using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.GetUser;

public sealed class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async ValueTask<Result<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByExternalIdAsync(request.ExternalId, cancellationToken);

        if (user is null)
        {
            return Result<UserDto>.NotFound($"User with external ID '{request.ExternalId}' not found.");
        }

        return Result<UserDto>.Success(UserDto.From(user));
    }
}
