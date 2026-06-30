using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>
/// Idempotent startup reconciler that seeds any code-referenced permission keys (from
/// <see cref="PermissionCatalog"/>) as <c>IsSystem</c> and warns about orphan system
/// permissions that are no longer referenced by any code manifest.
/// </summary>
/// <remarks>
/// Concurrent replicas are serialized by a PostgreSQL advisory lock so that a unique-index
/// violation on <c>Permission.Key</c> cannot occur under a race. The advisory lock is
/// intentionally skipped on non-Npgsql providers (e.g. the SQLite integration-test database)
/// because those environments run in-process and need no cross-instance coordination.
/// The seed/orphan logic runs regardless of provider.
/// </remarks>
public sealed class AuthorizationCatalogReconciler(
    IdentityDbContext dbContext,
    IEnumerable<IModulePermissionManifest> manifests,
    ILogger<AuthorizationCatalogReconciler> logger)
{
    private readonly IdentityDbContext _dbContext = dbContext;
    private readonly IEnumerable<IModulePermissionManifest> _manifests = manifests;
    private readonly ILogger<AuthorizationCatalogReconciler> _logger = logger;

    // FNV-1a-inspired stable 64-bit key: "AUTHZREC" in ASCII hex.
    private const long AdvisoryLockKey = 0x4155_5448_5A52_4543;

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var isNpgsql = _dbContext.Database.IsNpgsql();

        if (isNpgsql)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_lock({AdvisoryLockKey})", cancellationToken);
        }

        try
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
        finally
        {
            if (isNpgsql)
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({AdvisoryLockKey})", cancellationToken);
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
