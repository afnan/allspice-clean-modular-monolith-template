using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class ModuleRoleAssignmentRepository : RepositoryBase<ModuleRoleAssignment>, IModuleRoleAssignmentRepository
{
    private readonly IdentityDbContext _dbContext;

    public ModuleRoleAssignmentRepository(IdentityDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ModuleRoleAssignment>> GetActiveAssignmentsAsync(ExternalUserId userId, CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.ModuleRoleAssignments
            .Where(assignment => assignment.UserId.Value == userId.Value && assignment.RevokedUtc == null)
            .OrderBy(assignment => assignment.ModuleKey)
            .ThenBy(assignment => assignment.RoleKey)
            .ToListAsync(cancellationToken);

        return items;
    }

    public Task<ModuleRoleAssignment?> FindAssignmentAsync(ExternalUserId userId, string moduleKey, string roleKey, CancellationToken cancellationToken = default) =>
        _dbContext.ModuleRoleAssignments
            .FirstOrDefaultAsync(assignment =>
                assignment.UserId.Value == userId.Value &&
                assignment.ModuleKey == moduleKey &&
                assignment.RoleKey == roleKey,
                cancellationToken);
}


