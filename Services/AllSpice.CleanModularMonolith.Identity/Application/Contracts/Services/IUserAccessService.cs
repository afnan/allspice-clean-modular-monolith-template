namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;

public interface IUserAccessService
{
    Task<bool> CanAccessAsync(string externalUserId, CancellationToken cancellationToken = default);
}
