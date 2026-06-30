using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IRoleRepository : IRepository<Role>, IReadRepository<Role>
{
    Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
