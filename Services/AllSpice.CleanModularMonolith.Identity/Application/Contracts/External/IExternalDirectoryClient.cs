namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;

public interface IExternalDirectoryClient
{
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    Task InviteUserAsync(string email, string displayName, CancellationToken cancellationToken = default);
}


