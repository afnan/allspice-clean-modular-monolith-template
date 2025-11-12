namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;

/// <summary>
/// Abstraction over external identity directories (e.g., Authentik).
/// </summary>
public interface IExternalDirectoryClient
{
    /// <summary>
    /// Determines whether a user exists in the external directory.
    /// </summary>
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves the display name for the specified user.
    /// </summary>
    Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Sends an invitation for the specified user if supported by the directory.
    /// </summary>
    Task InviteUserAsync(string email, string displayName, CancellationToken cancellationToken = default);
}


