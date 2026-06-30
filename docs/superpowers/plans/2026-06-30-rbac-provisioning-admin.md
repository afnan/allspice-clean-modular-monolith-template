# RBAC Provisioning, Modules & Propagation — Implementation Plan (Plan B of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the seeded enforcement core (Plan A) fully dynamic and multi-replica-correct: self-declaring module permissions, an idempotent startup reconciler, config-driven first-admin bootstrap, Keycloak role sync, the runtime admin API, and near-instant cross-replica cache eviction via Redis pub-sub.

**Architecture:** Each module declares its permission keys via `IModulePermissionManifest`; a startup reconciler (guarded by `pg_advisory_lock`) seeds those + `Permissions.All` as `IsSystem` and warns on orphans. A config `BootstrapAdminRole` is auto-granted `authz.*`. `RoleSyncJob` upserts `Role` rows from Keycloak (degrading when unconfigured). The admin API edits the catalog + mappings at runtime; every mutation bumps `AuthzMapVersion` and publishes a best-effort Redis nudge so every replica evicts its in-memory map (60s TTL backstop; in-process when Redis is absent).

**Tech Stack:** .NET 10, ASP.NET Core authorization, FastEndpoints 8.2.0, EF Core 10.0.9 (Npgsql), Mediator 3.0.2, Quartz 3.18.1, StackExchange.Redis, xUnit 2.9.3 + Moq 4.20.72.

**Depends on:** Plan A (`2026-06-30-rbac-enforcement-core.md`). **Spec:** `2026-06-29-app-rbac-design.md` · **ADR:** `0008`.

## Global Constraints

Same as Plan A: 0 warnings, central package versions, bespoke repository per aggregate (ADR-0004), local UUID principal (ADR-0005), `Result.Forbidden()` → 403, staged repository writes committed by `TransactionBehavior`, Keycloak-optional boot (jobs degrade, `/health` Degraded), `module_roles` stays removed.

**Plan A interfaces consumed (exact):** `Permissions.All` / `Permissions.IsValidKey` / `Permissions.AuthzRead` / `Permissions.AuthzManage`; `IPermissionMapStore.GetMapAsync`; `IPermissionMapCache` (singleton, IMemoryCache key `"authz:map"`); `Permission.Create(key, description, isSystem)`; `Role.Create(key, description)`; `RolePermission.Create(roleId, permissionId)`; `AuthzMapVersion.Initial()/Bump()/Version`; `PermissionPolicy.For(key)`; `IdentityDbContext.{Permissions,Roles,RolePermissions,AuthzMapVersions}`.

---

## File Structure

**`Identity.Abstractions/Authorization/`:** `IModulePermissionManifest.cs`, `IAuthzCacheInvalidator.cs`.
**`Identity/Application/Contracts/Persistence/`:** `IPermissionRepository.cs`, `IRoleRepository.cs`, `IRolePermissionRepository.cs`, `IAuthzMapVersionRepository.cs`.
**`Identity/Infrastructure/Repositories/`:** `PermissionRepository.cs`, `RoleRepository.cs`, `RolePermissionRepository.cs`, `AuthzMapVersionRepository.cs`.
**`Identity/Infrastructure/Authorization/`:** `AuthorizationCatalogReconciler.cs`, `AuthorizationBootstrapper.cs`, `RedisAuthzCacheInvalidator.cs`, `InProcessAuthzCacheInvalidator.cs`, `AuthzCacheEvictionSubscriber.cs`; modify `PermissionMapCache.cs` (expose `Invalidate()`, TTL→60s).
**`Identity/Infrastructure/Services/KeycloakRoleClient.cs`:** add `GetAllRealmRolesAsync`.
**`Identity/Infrastructure/Jobs/RoleSyncJob.cs`:** new.
**`Identity/Infrastructure/Options/AuthorizationOptions.cs`:** new (`BootstrapAdminRole`).
**`Identity/Application/Features/Authorization/`:** queries + commands (+ validators).
**`Identity/Api/Endpoints/Authorization/`:** the 5 endpoints.
**`Identity/Infrastructure/Extensions/IdentityModuleExtensions.cs`:** register all of the above + the reconciler/bootstrapper startup hooks + Quartz `RoleSyncJob`.
**`Services/Notifications/.../NotificationsPermissionManifest.cs`:** example manifest (Task 9).
**Tests:** `Identity.Application.UnitTests/Authorization/*` + `Identity.Infrastructure.IntegrationTests/Authorization/*`.

---

## Task 1: Module permission manifest

**Files:**
- Create: `Shared/.../Identity.Abstractions/Authorization/IModulePermissionManifest.cs`
- Create: `Services/.../Identity/Infrastructure/Authorization/PermissionCatalog.cs`
- Test: `tests/.../Identity.Application.UnitTests/Authorization/PermissionCatalogTests.cs`

**Interfaces:**
- Produces: `IModulePermissionManifest { string ModuleKey; IReadOnlyCollection<PermissionDefinition> Permissions; }`, `PermissionDefinition(string Key, string Description)`, `PermissionCatalog.Collect(IEnumerable<IModulePermissionManifest>) : IReadOnlyCollection<PermissionDefinition>` (app-level `authz.*` + module keys, validated + deduped).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/.../Authorization/PermissionCatalogTests.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionCatalogTests
{
    private sealed class CmsManifest : IModulePermissionManifest
    {
        public string ModuleKey => "cms";
        public IReadOnlyCollection<PermissionDefinition> Permissions =>
        [
            new("cms.access", "Access CMS"),
            new("cms:articles.publish", "Publish articles"),
        ];
    }

    [Fact]
    public void Collect_includes_app_level_and_module_keys()
    {
        var all = PermissionCatalog.Collect([new CmsManifest()]);
        var keys = all.Select(d => d.Key).ToHashSet();
        Assert.Contains("authz.manage", keys);
        Assert.Contains("cms.access", keys);
        Assert.Contains("cms:articles.publish", keys);
    }

    [Fact]
    public void Collect_rejects_a_malformed_module_key()
    {
        var bad = new BadManifest();
        Assert.Throws<InvalidOperationException>(() => PermissionCatalog.Collect([bad]));
    }

    private sealed class BadManifest : IModulePermissionManifest
    {
        public string ModuleKey => "bad";
        public IReadOnlyCollection<PermissionDefinition> Permissions => [new("NOT VALID", "x")];
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// Identity.Abstractions/Authorization/IModulePermissionManifest.cs
namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public sealed record PermissionDefinition(string Key, string Description);

/// <summary>A module declares the permission keys it enforces. The reconciler seeds these as IsSystem.
/// Convention: a coarse "<c>{module}.access</c>" gate plus fine "<c>{module}:area.action</c>" keys.</summary>
public interface IModulePermissionManifest
{
    string ModuleKey { get; }
    IReadOnlyCollection<PermissionDefinition> Permissions { get; }
}
```

```csharp
// Identity/Infrastructure/Authorization/PermissionCatalog.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Combines app-level keys (Permissions.All) with every module manifest into the complete,
/// validated, de-duplicated code-referenced catalog.</summary>
public static class PermissionCatalog
{
    public static IReadOnlyCollection<PermissionDefinition> Collect(IEnumerable<IModulePermissionManifest> manifests)
    {
        var byKey = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);

        foreach (var key in Permissions.All)
        {
            byKey[key] = new PermissionDefinition(key, $"System permission {key}");
        }

        foreach (var manifest in manifests)
        {
            foreach (var def in manifest.Permissions)
            {
                if (!Permissions.IsValidKey(def.Key))
                {
                    throw new InvalidOperationException(
                        $"Module '{manifest.ModuleKey}' declares an invalid permission key '{def.Key}'.");
                }

                byKey[def.Key] = def; // last definition wins; duplicates collapse
            }
        }

        return byKey.Values.ToList();
    }
}
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): module permission manifest + catalog collector"`

---

## Task 2: Write repositories

**Files:**
- Create: `Identity/Application/Contracts/Persistence/IPermissionRepository.cs`, `IRoleRepository.cs`, `IRolePermissionRepository.cs`, `IAuthzMapVersionRepository.cs`
- Create: `Identity/Infrastructure/Repositories/PermissionRepository.cs`, `RoleRepository.cs`, `RolePermissionRepository.cs`, `AuthzMapVersionRepository.cs`
- Modify: `IdentityModuleExtensions.cs` (register the four)
- Test: `tests/.../Identity.Infrastructure.IntegrationTests/Authorization/AuthorizationRepositoriesTests.cs`

**Interfaces (exact):**
- `IPermissionRepository : IRepository<Permission>, IReadRepository<Permission>` + `Task<Permission?> GetByKeyAsync(string key, CancellationToken ct)`
- `IRoleRepository : IRepository<Role>, IReadRepository<Role>` + `Task<Role?> GetByKeyAsync(string key, CancellationToken ct)`
- `IRolePermissionRepository` + `Task<IReadOnlyList<RolePermission>> ListByRoleIdAsync(Guid roleId, CancellationToken ct)`, `void Add(RolePermission rp)`, `void RemoveRange(IEnumerable<RolePermission> rows)`
- `IAuthzMapVersionRepository` + `Task<AuthzMapVersion> GetTrackedAsync(CancellationToken ct)` (creates the singleton at version 0 if missing; returns a tracked instance so `Bump()` persists)

- [ ] **Step 1: Write the failing integration test** — round-trip a permission by key; bump the version via the tracked singleton.

```csharp
// tests/.../Authorization/AuthorizationRepositoriesTests.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class AuthorizationRepositoriesTests
{
    [Fact]
    public async Task Permission_round_trips_by_key()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();
        var repo = new PermissionRepository(ctx);
        await repo.AddAsync(Permission.Create("cms.access", "Access CMS", true), default);
        await ctx.SaveChangesAsync();

        var found = await repo.GetByKeyAsync("cms.access", default);
        Assert.NotNull(found);
        Assert.True(found!.IsSystem);
    }

    [Fact]
    public async Task Version_repo_creates_and_bumps_singleton()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();
        var repo = new AuthzMapVersionRepository(ctx);
        var v = await repo.GetTrackedAsync(default);
        v.Bump();
        await ctx.SaveChangesAsync();

        var reloaded = await new AuthzMapVersionRepository(ctx).GetTrackedAsync(default);
        Assert.Equal(1, reloaded.Version);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement contracts + impls**

```csharp
// Application/Contracts/Persistence/IPermissionRepository.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IPermissionRepository : IRepository<Permission>, IReadRepository<Permission>
{
    Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
```

```csharp
// Application/Contracts/Persistence/IRoleRepository.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IRoleRepository : IRepository<Role>, IReadRepository<Role>
{
    Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
```

```csharp
// Application/Contracts/Persistence/IRolePermissionRepository.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> ListByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    void Add(RolePermission rolePermission);
    void RemoveRange(IEnumerable<RolePermission> rows);
}
```

```csharp
// Application/Contracts/Persistence/IAuthzMapVersionRepository.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IAuthzMapVersionRepository
{
    /// <summary>Returns the tracked singleton (creating it at version 0 if absent) so Bump() persists on save.</summary>
    Task<AuthzMapVersion> GetTrackedAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// Infrastructure/Repositories/PermissionRepository.cs
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class PermissionRepository(IdentityDbContext dbContext)
    : EfRepository<IdentityDbContext, Permission>(dbContext), IPermissionRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _dbContext.Permissions.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
}
```

```csharp
// Infrastructure/Repositories/RoleRepository.cs
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class RoleRepository(IdentityDbContext dbContext)
    : EfRepository<IdentityDbContext, Role>(dbContext), IRoleRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    // EF.Functions.ILike for case-insensitive match (Npgsql); the unique index is on Key.
    public Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _dbContext.Roles.FirstOrDefaultAsync(r => r.Key.ToLower() == key.ToLower(), cancellationToken);
}
```

```csharp
// Infrastructure/Repositories/RolePermissionRepository.cs
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
```

```csharp
// Infrastructure/Repositories/AuthzMapVersionRepository.cs
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class AuthzMapVersionRepository(IdentityDbContext dbContext) : IAuthzMapVersionRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public async Task<AuthzMapVersion> GetTrackedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AuthzMapVersions.FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = AuthzMapVersion.Initial();
        _dbContext.AuthzMapVersions.Add(created);
        return created;
    }
}
```

- [ ] **Step 4: Register + run** — in `IdentityModuleExtensions` (near the existing `AddScoped<IUserRepository, UserRepository>()`): register the four (`IPermissionRepository`/`IRoleRepository`/`IRolePermissionRepository`/`IAuthzMapVersionRepository`). Run the test — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): write repositories for permissions/roles/mappings/version"`

---

## Task 3: Startup reconciler (idempotent seeding)

**Files:**
- Create: `Identity/Infrastructure/Authorization/AuthorizationCatalogReconciler.cs`
- Modify: `IdentityModuleExtensions.cs` (add `ReconcileAuthorizationCatalogAsync` startup hook)
- Test: `tests/.../Identity.Infrastructure.IntegrationTests/Authorization/AuthorizationCatalogReconcilerTests.cs`

**Interfaces:**
- Consumes: `PermissionCatalog`, `IPermissionRepository`, `IModulePermissionManifest[]`, `MigrationRunner`'s `pg_advisory_lock` helper.
- Produces: `AuthorizationCatalogReconciler.ReconcileAsync(CancellationToken)` — seeds missing code keys as `IsSystem`, logs each orphan DB permission, never deletes.

- [ ] **Step 1: Write the failing test** — run twice; the second run inserts nothing (idempotent), all catalog keys exist as `IsSystem`, and a pre-inserted non-code permission is left intact + warned.

```csharp
// tests/.../Authorization/AuthorizationCatalogReconcilerTests.cs
[Fact] public async Task Seeds_catalog_keys_as_system_and_is_idempotent() { /* run ReconcileAsync twice; count rows stable; authz.manage present + IsSystem */ }
[Fact] public async Task Leaves_orphan_db_permission_intact() { /* insert "custom.thing" (IsSystem=false); reconcile; still present */ }
```
(Concrete: after two `ReconcileAsync` calls, `ctx.Permissions.Count(p => p.Key == "authz.manage") == 1` and `IsSystem`; a pre-seeded `Permission.Create("custom.thing", "x", false)` still exists.)

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement** (advisory lock so concurrent replicas don't race the unique index; reuse the `MigrationRunner` Npgsql advisory-lock approach — open a connection, `pg_advisory_lock(<stable key>)`, reconcile, unlock).

```csharp
// Infrastructure/Authorization/AuthorizationCatalogReconciler.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class AuthorizationCatalogReconciler(
    IdentityDbContext dbContext,
    IEnumerable<IModulePermissionManifest> manifests,
    ILogger<AuthorizationCatalogReconciler> logger)
{
    private const long AdvisoryLockKey = 0x4155_5448_5A52_4543; // "AUTHZREC"

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_lock({AdvisoryLockKey})", cancellationToken);
        try
        {
            var catalog = PermissionCatalog.Collect(manifests);
            var existing = await dbContext.Permissions.ToDictionaryAsync(p => p.Key, cancellationToken);
            var codeKeys = catalog.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

            foreach (var def in catalog)
            {
                if (!existing.ContainsKey(def.Key))
                {
                    dbContext.Permissions.Add(Permission.Create(def.Key, def.Description, isSystem: true));
                    logger.LogInformation("Seeded system permission {Key}", def.Key);
                }
            }

            foreach (var orphan in existing.Values.Where(p => p.IsSystem && !codeKeys.Contains(p.Key)))
            {
                logger.LogWarning("System permission {Key} is no longer referenced by code", orphan.Key);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_unlock({AdvisoryLockKey})", cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Wire startup** — register `AddScoped<AuthorizationCatalogReconciler>()`; add an extension `ReconcileAuthorizationCatalogAsync(this WebApplication app)` that opens a scope and calls `ReconcileAsync`, invoked in the gateway startup right after `EnsureIdentityModuleDatabaseAsync`. Run the test — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): idempotent startup catalog reconciler (advisory-locked)"`

---

## Task 4: Config-driven first-admin bootstrap

**Files:**
- Create: `Identity/Infrastructure/Options/AuthorizationOptions.cs`
- Create: `Identity/Infrastructure/Authorization/AuthorizationBootstrapper.cs`
- Modify: `IdentityModuleExtensions.cs` (bind options, register, startup hook after reconciler)
- Test: `tests/.../Identity.Infrastructure.IntegrationTests/Authorization/AuthorizationBootstrapperTests.cs`

**Interfaces:**
- Consumes: `AuthorizationOptions.BootstrapAdminRole`, `IRoleRepository`, `IPermissionRepository`, `IRolePermissionRepository`, `IAuthzMapVersionRepository`.
- Produces: `AuthorizationBootstrapper.BootstrapAsync(CancellationToken)` — ensures `BootstrapAdminRole` (Role row + mapping to `authz.read`+`authz.manage`), idempotent; no-op when unset; bumps version once if it changed anything.

- [ ] **Step 1: Write the failing test** — with `BootstrapAdminRole = "platform-admin"`: after `BootstrapAsync`, the role maps to both authz perms; running again adds nothing; with the option null, nothing happens.

```csharp
[Fact] public async Task Grants_authz_perms_to_configured_role_idempotently() { /* run twice -> exactly 2 RolePermission rows for platform-admin */ }
[Fact] public async Task No_op_when_unset() { /* BootstrapAdminRole null -> 0 roles created */ }
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// Infrastructure/Options/AuthorizationOptions.cs
namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

public sealed class AuthorizationOptions
{
    public const string SectionName = "Authorization";

    /// <summary>Realm-role name auto-granted authz.read + authz.manage on startup. Null = no bootstrap.</summary>
    public string? BootstrapAdminRole { get; set; }
}
```

```csharp
// Infrastructure/Authorization/AuthorizationBootstrapper.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthzOptions = AllSpice.CleanModularMonolith.Identity.Infrastructure.Options.AuthorizationOptions;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class AuthorizationBootstrapper(
    IOptions<AuthzOptions> options,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IRolePermissionRepository rolePermissionRepository,
    IAuthzMapVersionRepository versionRepository,
    ILogger<AuthorizationBootstrapper> logger)
{
    private static readonly string[] AdminKeys = [Permissions.AuthzRead, Permissions.AuthzManage];

    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        var roleKey = options.Value.BootstrapAdminRole;
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return;
        }

        var role = await roleRepository.GetByKeyAsync(roleKey, cancellationToken);
        if (role is null)
        {
            role = Role.Create(roleKey, "Bootstrap admin role");
            await roleRepository.AddAsync(role, cancellationToken);
            logger.LogWarning(
                "Bootstrap admin role '{Role}' was not synced from Keycloak; created locally. Grants are inert until a user holds this realm role.",
                roleKey);
        }

        var existing = (await rolePermissionRepository.ListByRoleIdAsync(role.Id, cancellationToken))
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        var changed = false;
        foreach (var key in AdminKeys)
        {
            var permission = await permissionRepository.GetByKeyAsync(key, cancellationToken);
            if (permission is not null && !existing.Contains(permission.Id))
            {
                rolePermissionRepository.Add(RolePermission.Create(role.Id, permission.Id));
                changed = true;
            }
        }

        if (changed)
        {
            (await versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        }
    }
}
```

- [ ] **Step 4: Wire** — bind `AuthorizationOptions` from the `"Authorization"` section; register `AddScoped<AuthorizationBootstrapper>()`; call `BootstrapAsync` in the same startup scope right after the reconciler (so `authz.*` permissions already exist). The whole startup step saves once. Run the test — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): config-driven first-admin bootstrap"`

---

## Task 5: AuthzMapVersion bump + Redis pub-sub cache eviction

**Files:**
- Create: `Shared/.../Identity.Abstractions/Authorization/IAuthzCacheInvalidator.cs`
- Create: `Identity/Infrastructure/Authorization/RedisAuthzCacheInvalidator.cs`, `InProcessAuthzCacheInvalidator.cs`, `AuthzCacheEvictionSubscriber.cs`
- Modify: `Identity/Infrastructure/Authorization/PermissionMapCache.cs` (TTL→60s; add `Invalidate()`)
- Modify: `IdentityModuleExtensions.cs` (register invalidator by Redis presence; register subscriber hosted service; register `IConnectionMultiplexer` when a redis connection string exists)
- Modify: `Directory.Packages.props` (add `StackExchange.Redis` if absent)
- Test: `tests/.../Identity.Application.UnitTests/Authorization/PermissionMapCacheInvalidationTests.cs`

**Interfaces:**
- Produces: `IAuthzCacheInvalidator.InvalidateAsync(CancellationToken)`; `PermissionMapCache.Invalidate()` (removes the `"authz:map"` entry).

- [ ] **Step 1: Write the failing test** — `Invalidate()` forces the next `GetAsync` to reload (store called again).

```csharp
// tests/.../Authorization/PermissionMapCacheInvalidationTests.cs
[Fact]
public async Task Invalidate_forces_reload_on_next_get()
{
    // arrange: a PermissionMapCache over a scope factory whose IPermissionMapStore counts GetMapAsync calls
    // act: GetAsync (loads, count=1) -> GetAsync (cached, count=1) -> Invalidate() -> GetAsync (reloads, count=2)
    // assert: store.CallCount == 2
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (`Invalidate` missing).

- [ ] **Step 3: Implement**

```csharp
// Identity.Abstractions/Authorization/IAuthzCacheInvalidator.cs
namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Signals every replica to drop its cached role→permission map. Redis pub-sub when configured;
/// in-process eviction when Redis is absent (single node).</summary>
public interface IAuthzCacheInvalidator
{
    Task InvalidateAsync(CancellationToken cancellationToken);
}
```

Add to `PermissionMapCache` (Plan A): change `Ttl` to `TimeSpan.FromSeconds(60)` and add:
```csharp
public void Invalidate() => _cache.Remove(CacheKey); // CacheKey == "authz:map"
```
(Promote `IPermissionMapCache` with `void Invalidate();`.)

```csharp
// Infrastructure/Authorization/InProcessAuthzCacheInvalidator.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Used when Redis is not configured: evicts this process's cache directly (single-node correctness).</summary>
public sealed class InProcessAuthzCacheInvalidator(IPermissionMapCache cache) : IAuthzCacheInvalidator
{
    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }
}
```

```csharp
// Infrastructure/Authorization/RedisAuthzCacheInvalidator.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using StackExchange.Redis;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Publishes a best-effort nudge; ALL subscribers (including this node) evict. Failure is swallowed —
/// the 60s TTL backstop guarantees eventual convergence.</summary>
public sealed class RedisAuthzCacheInvalidator(IConnectionMultiplexer redis) : IAuthzCacheInvalidator
{
    public const string Channel = "authz:invalidate";

    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), "1");
        }
        catch
        {
            // best-effort: TTL backstop covers a lost nudge
        }
    }
}
```

```csharp
// Infrastructure/Authorization/AuthzCacheEvictionSubscriber.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Subscribes to the invalidation channel and evicts this replica's in-memory map on each message.</summary>
public sealed class AuthzCacheEvictionSubscriber(IConnectionMultiplexer redis, IPermissionMapCache cache) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
        => await redis.GetSubscriber().SubscribeAsync(
            RedisChannel.Literal(RedisAuthzCacheInvalidator.Channel), (_, _) => cache.Invalidate());

    public async Task StopAsync(CancellationToken cancellationToken)
        => await redis.GetSubscriber().UnsubscribeAsync(RedisChannel.Literal(RedisAuthzCacheInvalidator.Channel));
}
```

- [ ] **Step 4: Register by Redis presence + run** — in `IdentityModuleExtensions`, read the `redis` connection string (same key the gateway uses). If present: register `IConnectionMultiplexer` (`ConnectionMultiplexer.Connect(...)`, or reuse the Aspire `AddRedisClient("redis")` registration), `AddSingleton<IAuthzCacheInvalidator, RedisAuthzCacheInvalidator>()`, and `AddHostedService<AuthzCacheEvictionSubscriber>()`. Else: `AddSingleton<IAuthzCacheInvalidator, InProcessAuthzCacheInvalidator>()`. Run the unit test — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): Redis pub-sub cache eviction + 60s TTL backstop"`

---

## Task 6: Keycloak role sync

**Files:**
- Modify: `Identity/Infrastructure/Services/KeycloakRoleClient.cs` (add `GetAllRealmRolesAsync`)
- Create: `Identity/Infrastructure/Jobs/RoleSyncJob.cs`
- Modify: `IdentityModuleExtensions.cs` (register `KeycloakRoleClient` + the Quartz job)
- Test: `tests/.../Identity.Application.UnitTests/Authorization/RoleSyncJobTests.cs`

**Interfaces:**
- Produces: `KeycloakRoleClient.GetAllRealmRolesAsync(CancellationToken) : Task<List<string>>`; `RoleSyncJob : IJob` with `JobIdentity = "RoleSyncJob"`.

- [ ] **Step 1: Write the failing test** — mirror `KeycloakUserSyncJobTests`: when `!IsAdminConfigured`, the job creates no scope and does nothing.

```csharp
// tests/.../Authorization/RoleSyncJobTests.cs
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class RoleSyncJobTests
{
    [Fact]
    public async Task Does_nothing_when_keycloak_is_not_configured()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var keycloakOptions = Options.Create(new KeycloakOptions { Realm = "demo" }); // not admin-configured

        var job = new RoleSyncJob(scopeFactory.Object, keycloakOptions, NullLogger<RoleSyncJob>.Instance);
        await job.Execute(new Mock<IJobExecutionContext>().Object);

        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3a: Add the client method** (after `GetUserRealmRolesAsync`):

```csharp
public async Task<List<string>> GetAllRealmRolesAsync(CancellationToken cancellationToken = default)
{
    var response = await _httpClient.GetAsync("roles", cancellationToken);
    response.EnsureSuccessStatusCode();
    using var doc = await response.ReadJsonAsync(cancellationToken);
    return doc.RootElement.EnumerateArray()
        .Select(r => r.GetProperty("name").GetString() ?? string.Empty)
        .Where(name => !string.IsNullOrEmpty(name))
        .ToList();
}
```

- [ ] **Step 3b: Implement the job** (mirrors `KeycloakUserSyncJob` guard + scope pattern):

```csharp
// Infrastructure/Jobs/RoleSyncJob.cs
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class RoleSyncJob(
    IServiceScopeFactory scopeFactory,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<RoleSyncJob> logger) : IJob
{
    public const string JobIdentity = "RoleSyncJob";

    public async Task Execute(IJobExecutionContext context)
    {
        if (!keycloakOptions.Value.IsAdminConfigured)
        {
            logger.LogDebug("Keycloak is not linked yet — skipping role sync.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<KeycloakRoleClient>();
        var roleRepo = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        try
        {
            var realmRoles = await client.GetAllRealmRolesAsync(context.CancellationToken);
            foreach (var roleName in realmRoles)
            {
                if (await roleRepo.GetByKeyAsync(roleName, context.CancellationToken) is null)
                {
                    await roleRepo.AddAsync(Role.Create(roleName, null), context.CancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Synced {Count} realm roles", realmRoles.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            logger.LogWarning(ex, "Role sync failed transiently; will retry on next trigger.");
        }
    }
}
```

- [ ] **Step 4: Register + run** — register `KeycloakRoleClient` against the keycloak HTTP client (mirror `AddHttpClient<KeycloakDirectoryClient>`), and add the Quartz job/trigger next to `KeycloakUserSyncJob` (reuse the same cron config pattern). Run the test — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): Keycloak role sync job (degrades when unconfigured)"`

---

## Task 7: Admin API — read side

**Files:**
- Create: `Identity/Application/Features/Authorization/Queries/ListPermissions/*`, `ListRoles/*`, `GetRolePermissions/*`
- Create: `Identity/Api/Endpoints/Authorization/ListPermissionsEndpoint.cs`, `ListRolesEndpoint.cs`, `GetRolePermissionsEndpoint.cs`
- Test: `tests/.../Identity.Application.UnitTests/Authorization/ListPermissionsQueryHandlerTests.cs` + an integration test for one endpoint's 403/200

**Interfaces:**
- Produces: `ListPermissionsQuery() : IRequest<Result<IReadOnlyList<PermissionDto>>>` etc.; endpoints gated by `Policies(PermissionPolicy.For(Permissions.AuthzRead))`.

- [ ] **Step 1: Write the failing handler test** — `ListPermissionsQueryHandler` returns all permissions ordered by key.

```csharp
[Fact] public async Task Lists_permissions_ordered_by_key() { /* repo returns 2 perms -> handler returns ordered DTOs */ }
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement** the query + handler (Mediator `IRequestHandler<,>` returning `ValueTask<Result<...>>`, depends on `IReadRepository<Permission>` via `IPermissionRepository`), the `PermissionDto(Guid Id, string Key, string Description, bool IsSystem)`, and the endpoint:

```csharp
// Api/Endpoints/Authorization/ListPermissionsEndpoint.cs
public override void Configure()
{
    Get("/api/identity/authz/permissions");
    Policies(PermissionPolicy.For(Permissions.AuthzRead));
    Tags("Authorization");
}
```
(Repeat for `ListRolesEndpoint` and `GetRolePermissionsEndpoint`, all gated by `authz.read`. Mirror `ListUsersEndpoint`'s mediator + `ToProblem()` mapping.)

- [ ] **Step 4: Run handler test + add an integration test** asserting `GET /api/identity/authz/permissions` returns 403 without `authz.read` and 200 with it (reuse Plan A's e2e host helper). Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): admin API read endpoints (permissions, roles, mappings)"`

---

## Task 8: Admin API — write side

**Files:**
- Create: `Identity/Application/Features/Authorization/Commands/CreatePermission/*`, `DeletePermission/*`, `SetRolePermissions/*` (command + handler + validator each)
- Create: `Identity/Api/Endpoints/Authorization/CreatePermissionEndpoint.cs`, `DeletePermissionEndpoint.cs`, `SetRolePermissionsEndpoint.cs`
- Test: `tests/.../Identity.Application.UnitTests/Authorization/DeletePermissionCommandHandlerTests.cs`, `SetRolePermissionsCommandHandlerTests.cs`

**Interfaces:**
- Produces commands (all `IRequest<Result>, ITransactional`): `CreatePermissionCommand(string Key, string Description)`, `DeletePermissionCommand(Guid Id)`, `SetRolePermissionsCommand(string RoleKey, IReadOnlyList<string> PermissionKeys)`. Each mutating handler bumps `AuthzMapVersion` and calls `IAuthzCacheInvalidator.InvalidateAsync`. Endpoints gated by `authz.manage`.

- [ ] **Step 1: Write the failing tests**

```csharp
// DeletePermissionCommandHandlerTests.cs
[Fact] public async Task Deleting_a_system_permission_is_forbidden()
{ /* repo.GetById returns Permission with IsSystem=true -> handler returns Result.Forbidden */ }

[Fact] public async Task Deleting_a_custom_permission_succeeds_and_invalidates()
{ /* IsSystem=false -> repo.Delete called, version bumped, invalidator.InvalidateAsync called once */ }

// SetRolePermissionsCommandHandlerTests.cs
[Fact] public async Task Replaces_mapping_bumps_version_and_invalidates()
{ /* removes old rows, adds new, bump, invalidate once */ }
```

- [ ] **Step 2: Run to verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement** — e.g. the delete handler:

```csharp
// Commands/DeletePermission/DeletePermissionCommandHandler.cs
public async ValueTask<Result> Handle(DeletePermissionCommand command, CancellationToken cancellationToken)
{
    var permission = await _permissionRepository.GetByIdAsync(command.Id, cancellationToken);
    if (permission is null)
    {
        return Result.NotFound();
    }

    if (permission.IsSystem)
    {
        return Result.Forbidden(); // code-referenced keys are deletion-protected (ADR-0008)
    }

    await _permissionRepository.DeleteAsync(permission, cancellationToken);
    (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
    await _cacheInvalidator.InvalidateAsync(cancellationToken);
    return Result.Success();
}
```
`SetRolePermissionsCommandHandler`: `GetByKeyAsync` the role (get-or-create via sync is separate; here require it exists → `Result.NotFound()` if missing), `ListByRoleIdAsync`, `RemoveRange(existing)`, resolve each key via `IPermissionRepository.GetByKeyAsync` (invalid/unknown key → `Result.Invalid`), `Add` new `RolePermission`s, bump version, `InvalidateAsync`. `CreatePermissionCommandValidator` rejects `!Permissions.IsValidKey(Key)`. Endpoints gated `Policies(PermissionPolicy.For(Permissions.AuthzManage))`; map `Result` via `ToProblem()` (Forbidden→403, Invalid→400).

- [ ] **Step 4: Run the tests** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): admin API write endpoints (create/delete permission, set role mapping)"`

---

## Task 9: Arch-fitness test + example module manifest

**Files:**
- Create: `Services/Notifications/.../Authorization/NotificationsPermissionManifest.cs` (+ register it; gate one Notifications endpoint with `notifications:preferences.manage` as a worked example)
- Create: `tests/.../<arch-test-project>/Authorization/PermissionKeyConsistencyTests.cs`
- Test: the arch test itself

**Interfaces:**
- Produces: a real second manifest proving the multi-module path; an arch-fitness test pinning key consistency.

- [ ] **Step 1: Write the failing arch test** — every manifest key and `Permissions.All` key is well-formed and unique; no two manifests declare the same key with different descriptions (collision check); every `HasPermissionAttribute` key across endpoint assemblies is in the collected catalog.

```csharp
// PermissionKeyConsistencyTests.cs
[Fact]
public void All_catalog_keys_are_valid_and_unique()
{
    var manifests = DiscoverManifests(); // reflection over IModulePermissionManifest impls in module assemblies
    var catalog = PermissionCatalog.Collect(manifests);
    Assert.All(catalog, d => Assert.True(Permissions.IsValidKey(d.Key), $"Invalid key {d.Key}"));
    Assert.Equal(catalog.Select(d => d.Key).Distinct().Count(), catalog.Count);
}

[Fact]
public void Every_HasPermission_attribute_key_is_declared()
{
    var declared = PermissionCatalog.Collect(DiscoverManifests()).Select(d => d.Key).ToHashSet();
    foreach (var (type, key) in EndpointPermissionKeys()) // reflect HasPermissionAttribute on endpoint types
    {
        Assert.True(declared.Contains(key), $"{type.Name} requires undeclared permission '{key}'");
    }
}
```
(`EndpointPermissionKeys` reflects over endpoint types for `HasPermissionAttribute`. FastEndpoints that call `Policies(PermissionPolicy.For(...))` in `Configure()` are covered by the convention that a module's keys come from its manifest; document this in `AGENTS.md` as a golden rule so endpoint authors add the key to the manifest.)

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (no manifest discovered / Notifications manifest missing).

- [ ] **Step 3: Implement the example manifest + register it**

```csharp
// Services/Notifications/.../Authorization/NotificationsPermissionManifest.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Authorization;

public sealed class NotificationsPermissionManifest : IModulePermissionManifest
{
    public string ModuleKey => "notifications";
    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("notifications.access", "Access the notifications module"),
        new("notifications:preferences.manage", "Manage notification preferences"),
    ];
}
```
Register in `NotificationsModuleExtensions`: `builder.Services.AddSingleton<IModulePermissionManifest, NotificationsPermissionManifest>();`. Gate `UpsertNotificationPreferenceEndpoint` (or equivalent) with `Policies(PermissionPolicy.For("notifications:preferences.manage"))` as the worked example. (Register the Identity-side keys too: add an `AuthzPermissionManifest`/`AddSingleton<IModulePermissionManifest>` for the `authz.*` app keys if you prefer them manifest-declared rather than via `Permissions.All`.)

- [ ] **Step 4: Run the arch test** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(authz): arch-fitness key consistency test + Notifications manifest example"`

---

## Self-Review

**Spec coverage (Plan B scope):**
- §2a manifest + two-tier module gate — Tasks 1, 9 (manifest + worked Notifications gate). ✓
- §3 role sync — Task 6 (`GetAllRealmRolesAsync` + `RoleSyncJob`, degrades). ✓
- §4 propagation (version bump + Redis pub-sub + 60s TTL + in-process fallback) — Task 5 + bumps in Tasks 4, 8. ✓
- §7 admin API + bootstrap + IsSystem-delete-forbidden — Tasks 4, 7, 8. ✓
- §8 reconciler (idempotent, advisory-lock, orphan warnings) + arch-test — Tasks 3, 9. ✓
- §9 criticals: reconciler idempotent across replicas (Task 3), Keycloak-unconfigured degrade (Task 6), bootstrap idempotent/no-op (Task 4), DELETE IsSystem→403 (Task 8). ✓

**Placeholder scan:** Tasks 7-9 give endpoint `Configure()` + handler shapes and concrete assertions but compress repetitive query/DTO/validator boilerplate behind "mirror `ListUsersEndpoint` / `UpsertNotificationPreference*`" references to real, existing files. The reflection helpers `DiscoverManifests` / `EndpointPermissionKeys` (Task 9) are described, not coded — the implementer wires them with the project's existing arch-test reflection utilities (ADR-0007). Flagged rather than inventing a discovery API.

**Type consistency:** `IAuthzCacheInvalidator.InvalidateAsync`, `IPermissionMapCache.Invalidate`, `IAuthzMapVersionRepository.GetTrackedAsync`, `IRolePermissionRepository.{ListByRoleIdAsync,Add,RemoveRange}`, `PermissionPolicy.For`, `Permissions.{AuthzRead,AuthzManage}` — consistent with Plan A and across Plan B tasks. ✓

**Open items for the implementer:** confirm the `IConnectionMultiplexer` registration source (Aspire `AddRedisClient("redis")` vs explicit `ConnectionMultiplexer.Connect`); confirm `KeycloakRoleClient` DI registration (currently constructed within the directory client) before `RoleSyncJob` resolves it; fill the arch-test reflection helpers from the existing arch-test project.

---

## Execution Handoff

Plans A and B are both written and committed. Recommended order: **execute Plan A end-to-end, then Plan B** (B's code assumes A's interfaces compile and behave).

Two execution options (per plan):
1. **Subagent-Driven (recommended)** — fresh subagent per task + review checkpoint between tasks (superpowers:subagent-driven-development).
2. **Inline Execution** — batch with checkpoints (superpowers:executing-plans).
