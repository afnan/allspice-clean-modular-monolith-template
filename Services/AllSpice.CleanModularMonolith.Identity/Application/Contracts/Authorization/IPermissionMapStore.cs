namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;

/// <summary>Reads the full role→permission map from the store. Scoped (uses the module DbContext).</summary>
public interface IPermissionMapStore
{
    Task<PermissionMap> GetMapAsync(CancellationToken cancellationToken);
}
