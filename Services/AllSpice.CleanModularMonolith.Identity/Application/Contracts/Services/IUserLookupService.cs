namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;

public interface IUserLookupService
{
    Task<string?> GetExternalIdByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default);
    Task<string?> GetDisplayNameByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default);
    Task<string?> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetLocalIdByExternalIdAsync(string externalUserId, CancellationToken cancellationToken = default);
    Task<(string? Email, string? FirstName)> GetUserContactByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default);
}
