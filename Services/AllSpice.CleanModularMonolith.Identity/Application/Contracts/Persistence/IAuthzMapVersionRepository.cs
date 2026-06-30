using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IAuthzMapVersionRepository
{
    /// <summary>Returns the tracked singleton (creating it at version 0 if absent) so Bump() persists on save.</summary>
    Task<AuthzMapVersion> GetTrackedAsync(CancellationToken cancellationToken = default);
}
