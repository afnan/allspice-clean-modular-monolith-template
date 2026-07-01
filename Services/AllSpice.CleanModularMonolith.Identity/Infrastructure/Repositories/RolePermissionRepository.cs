using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class RolePermissionRepository(IdentityDbContext dbContext) : IRolePermissionRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<RolePermission>> ListByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _dbContext.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);

    public void Add(RolePermission rolePermission) => _dbContext.RolePermissions.Add(rolePermission);

    public void RemoveRange(IEnumerable<RolePermission> rows) => _dbContext.RolePermissions.RemoveRange(rows);
}
