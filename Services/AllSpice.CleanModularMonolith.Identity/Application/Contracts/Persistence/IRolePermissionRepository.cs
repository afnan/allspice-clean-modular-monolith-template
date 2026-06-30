using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> ListByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    void Add(RolePermission rolePermission);
    void RemoveRange(IEnumerable<RolePermission> rows);
}
