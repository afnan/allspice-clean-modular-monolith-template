using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IPermissionRepository : IRepository<Permission>, IReadRepository<Permission>
{
    Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads only the permissions whose <see cref="Permission.Id"/> is in <paramref name="ids"/>,
    /// filtering in the database rather than loading the full permission set to filter in memory.
    /// </summary>
    Task<IReadOnlyList<Permission>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
