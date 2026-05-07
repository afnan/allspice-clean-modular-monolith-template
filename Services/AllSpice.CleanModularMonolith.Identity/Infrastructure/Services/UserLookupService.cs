using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.Users;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Resolves user information by local or external identifiers.
/// Also implements <see cref="IUserExternalIdResolver"/> for cross-module use.
/// </summary>
public sealed class UserLookupService : IUserLookupService, IUserExternalIdResolver
{
    private readonly IReadRepository<User> _users;
    private readonly IExternalDirectoryClient _directoryClient;

    public UserLookupService(IReadRepository<User> users, IExternalDirectoryClient directoryClient)
    {
        _users = users;
        _directoryClient = directoryClient;
    }

    public async Task<string?> GetExternalIdByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(localUserId, cancellationToken);
        return user?.ExternalId.Value;
    }

    public async Task<string?> GetDisplayNameByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(localUserId, cancellationToken);
        return user?.DisplayName;
    }

    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FirstOrDefaultAsync(new UserByExternalIdSpec(userId), cancellationToken);
        if (user is not null)
        {
            return user.DisplayName;
        }

        return await _directoryClient.GetUserDisplayNameAsync(userId, cancellationToken);
    }

    public async Task<Guid?> GetLocalIdByExternalIdAsync(string externalUserId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FirstOrDefaultAsync(new UserByExternalIdSpec(externalUserId), cancellationToken);
        return user?.Id;
    }

    public async Task<(string? Email, string? FirstName)> GetUserContactByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(localUserId, cancellationToken);
        return user is not null ? (user.Email, user.FirstName) : (null, null);
    }
}
