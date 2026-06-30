using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IPermissionRepository : IRepository<Permission>, IReadRepository<Permission>
{
    Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
