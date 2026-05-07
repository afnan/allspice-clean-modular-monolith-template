using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.Users;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Determines whether a user identified by their external (Keycloak) ID has access to the system.
/// </summary>
public sealed class UserAccessService : IUserAccessService
{
    private readonly IReadRepository<User> _users;

    public UserAccessService(IReadRepository<User> users)
    {
        _users = users;
    }

    public async Task<bool> CanAccessAsync(string externalUserId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FirstOrDefaultAsync(new UserByExternalIdSpec(externalUserId), cancellationToken);
        return user is { IsActive: true };
    }
}
