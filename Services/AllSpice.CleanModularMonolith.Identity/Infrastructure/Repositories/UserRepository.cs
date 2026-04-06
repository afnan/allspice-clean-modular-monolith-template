using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class UserRepository : RepositoryBase<User>, IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalId.Value == externalId, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail.ToLower(), cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _dbContext.Users
            .Where(u => idList.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByExternalIdsAsync(IEnumerable<string> externalIds, CancellationToken cancellationToken = default)
    {
        var idList = externalIds.ToList();
        return await _dbContext.Users
            .Where(u => idList.Contains(u.ExternalId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> ListActivePagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.Where(u => u.IsActive).OrderBy(u => u.Username);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<Dictionary<string, User>> GetAllIndexedByExternalIdAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Users
            .ToDictionaryAsync(u => u.ExternalId.Value, cancellationToken);
}
