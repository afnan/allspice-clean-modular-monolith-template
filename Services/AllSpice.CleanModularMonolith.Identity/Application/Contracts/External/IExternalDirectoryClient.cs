namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;

/// <summary>
/// Abstraction over external identity directories (e.g., Keycloak Admin REST API).
/// <para>
/// Intentionally a broad admin toolkit. Today the app only consumes the read paths
/// (<see cref="GetUserDisplayNameAsync"/> via UserLookupService, <see cref="GetUsersPagedAsync"/> via the
/// sync job) — this template is auth-agnostic and does NOT create users or manage passwords itself (the IdP
/// provisions users, directly or via SSO/SAML). The write/admin methods below are retained as ready-made
/// building blocks for consumers who do want app-driven directory operations; they are not dead code from a
/// removed feature.
/// </para>
/// </summary>
public interface IExternalDirectoryClient
{
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> CreateUserAsync(string email, string firstName, string lastName, string username, CancellationToken cancellationToken = default);
    Task<string> CreateUserWithTempPasswordAsync(string email, string firstName, string lastName, string username, string tempPassword, CancellationToken cancellationToken = default);
    Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task RevokeRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task ResetTemporaryPasswordAsync(string userId, string tempPassword, CancellationToken cancellationToken = default);
    Task UpdateUserNameAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken = default);
    Task<List<ExternalUser>> GetUsersPagedAsync(int first, int max, CancellationToken cancellationToken = default);
    Task<List<string>> GetUserRealmRolesAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed record ExternalUser(string Id, string Username, string? Email, string? FirstName, string? LastName, bool Enabled);
