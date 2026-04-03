namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;

/// <summary>
/// Abstraction over external identity directories (e.g., Keycloak Admin REST API).
/// </summary>
public interface IExternalDirectoryClient
{
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    Task InviteUserAsync(string email, string displayName, CancellationToken cancellationToken = default);
    Task<string> CreateUserAsync(string email, string firstName, string lastName, string username, CancellationToken cancellationToken = default);
    Task<string> CreateUserWithTempPasswordAsync(string email, string firstName, string lastName, string username, string tempPassword, CancellationToken cancellationToken = default);
    Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task RevokeRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task ResetTemporaryPasswordAsync(string userId, string tempPassword, CancellationToken cancellationToken = default);
    Task UpdateUserNameAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken = default);
    Task<List<ExternalUser>> GetUsersPagedAsync(int first, int max, CancellationToken cancellationToken = default);
    Task<List<string>> GetUserRealmRolesAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed record ExternalUser(string Id, string Username, string? Email, string? FirstName, string? LastName, bool Enabled);
