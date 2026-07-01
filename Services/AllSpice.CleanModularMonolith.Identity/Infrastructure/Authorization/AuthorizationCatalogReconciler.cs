using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>
/// Idempotent startup reconciler that seeds any code-referenced permission keys (from
/// <see cref="PermissionCatalog"/>) as <c>IsSystem</c> and warns about orphan system
/// permissions that are no longer referenced by any code manifest.
/// </summary>
/// <remarks>
/// Concurrent replicas are serialized by the caller
/// (<see cref="AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions.IdentityModuleExtensions.ReconcileAuthorizationCatalogAsync"/>),
/// which acquires a PostgreSQL advisory lock that spans both this reconcile seed and the
/// subsequent bootstrap. The seed/orphan logic runs regardless of database provider.
/// </remarks>
public sealed class AuthorizationCatalogReconciler(
    IdentityDbContext dbContext,
    IEnumerable<IModulePermissionManifest> manifests,
    ILogger<AuthorizationCatalogReconciler> logger)
{
    private readonly IdentityDbContext _dbContext = dbContext;
    private readonly IEnumerable<IModulePermissionManifest> _manifests = manifests;
    private readonly ILogger<AuthorizationCatalogReconciler> _logger = logger;

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var catalog = PermissionCatalog.Collect(_manifests);
        var existing = await _dbContext.Permissions.ToDictionaryAsync(p => p.Key, cancellationToken);
        var codeKeys = catalog.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var def in catalog)
        {
            if (!existing.ContainsKey(def.Key))
            {
                _dbContext.Permissions.Add(Permission.Create(def.Key, def.Description, isSystem: true));
                _logger.LogInformation("Seeded system permission {Key}", def.Key);
            }
        }

        foreach (var orphan in existing.Values.Where(p => p.IsSystem && !codeKeys.Contains(p.Key)))
        {
            _logger.LogWarning("System permission {Key} is no longer referenced by code", orphan.Key);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
