using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Resolves user information by local or external identifiers.
/// Also implements <see cref="IUserExternalIdResolver"/> for cross-module use.
/// </summary>
public sealed class UserLookupService : IUserLookupService, IUserExternalIdResolver
{
    private readonly IUserRepository _userRepository;
    private readonly IExternalDirectoryClient _directoryClient;

    public UserLookupService(IUserRepository userRepository, IExternalDirectoryClient directoryClient)
    {
        _userRepository = userRepository;
        _directoryClient = directoryClient;
    }

    public async Task<string?> GetExternalIdByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(localUserId, cancellationToken);
        return user?.ExternalId.Value;
    }

    public async Task<string?> GetDisplayNameByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(localUserId, cancellationToken);
        return user?.DisplayName;
    }

    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Try local first
        var user = await _userRepository.GetByExternalIdAsync(userId, cancellationToken);
        if (user is not null)
        {
            return user.DisplayName;
        }

        // Fall back to Keycloak
        return await _directoryClient.GetUserDisplayNameAsync(userId, cancellationToken);
    }

    public async Task<Guid?> GetLocalIdByExternalIdAsync(string externalUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByExternalIdAsync(externalUserId, cancellationToken);
        return user?.Id;
    }

    public async Task<(string? Email, string? FirstName)> GetUserContactByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(localUserId, cancellationToken);
        return user is not null ? (user.Email, user.FirstName) : (null, null);
    }
}
