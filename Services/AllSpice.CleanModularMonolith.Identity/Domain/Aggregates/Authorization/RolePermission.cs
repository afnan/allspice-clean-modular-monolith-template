using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>Join row mapping a <see cref="Role"/> to a <see cref="Permission"/>. Not an aggregate root;
/// read by the map store, mutated by the admin API (Plan B).</summary>
public sealed class RolePermission : Entity
{
    private RolePermission() { }

    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId) => new(roleId, permissionId);
}
