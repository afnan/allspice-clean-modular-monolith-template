using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IUserRepository : IRepository<User>, IReadRepository<User>
{
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByExternalIdsAsync(IEnumerable<string> externalIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, User>> GetAllIndexedByExternalIdAsync(CancellationToken cancellationToken = default);
}
