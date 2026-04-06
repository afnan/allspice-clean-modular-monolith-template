using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Determines whether a user identified by their external (Keycloak) ID has access to the system.
/// </summary>
public sealed class UserAccessService : IUserAccessService
{
    private readonly IUserRepository _userRepository;

    public UserAccessService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> CanAccessAsync(string externalUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByExternalIdAsync(externalUserId, cancellationToken);
        return user is { IsActive: true };
    }
}
