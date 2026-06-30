namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Tenant-aware identity for resource rules. Built from ICurrentUserContext (local UUID). Rules that
/// also need permission checks inject ICurrentUserPermissions and await HasPermissionAsync.</summary>
public interface IAuthorizationContext
{
    Guid? UserId { get; }
    string TenantId { get; }
}
