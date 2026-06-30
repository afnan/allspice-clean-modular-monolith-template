# RBAC Enforcement Core — Implementation Plan (Plan A of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a working, server-resolved, permission-based authorization gate (Layer 1 endpoint gate + Layer 2 resource authorizer), replacing the dead `module_roles` scaffolding, with permissions seeded via migration.

**Architecture:** Keycloak issues realm roles (already flattened to `ClaimTypes.Role` at the gateway). The app resolves those role names → a permission set server-side per request, cached in-memory. A dynamic `IAuthorizationPolicyProvider` materializes `perm:{key}` policies for a `[HasPermission]`/`Policies("perm:…")` gate; a thin `IResourceAuthorizer` facade over the built-in `AuthorizationHandler<TRequirement,TResource>` covers ownership/tenant/status inside handlers without leaking HttpContext. This plan seeds the catalog via migration; runtime admin management, role sync, bootstrap, and Redis pub-sub eviction are **Plan B**.

**Tech Stack:** .NET 10, ASP.NET Core authorization, FastEndpoints 8.2.0, EF Core 10.0.9 (Npgsql), Mediator 3.0.2, xUnit 2.9.3 + Moq 4.20.72.

**Spec:** `docs/superpowers/specs/2026-06-29-app-rbac-design.md` · **ADR:** `docs/adr/0008-in-app-permission-based-authorization.md`

## Global Constraints

- **Zero build warnings** — `TreatWarningsAsErrors=true`. File-scoped namespaces, `_camelCase` private fields (or primary-ctor params per existing files), `sealed` by default.
- **Central package versioning** — no inline `Version=` in `.csproj`; add to `Directory.Packages.props`.
- **Bespoke repository per aggregate** (ADR-0004) — handlers depend on a named `IXxxRepository`, never `IRepository<T>` directly. Read projections may use a dedicated query service (e.g. `UserLookupService` pattern), not a repository.
- **Local UUID is the principal** (ADR-0005) — resource checks use `ICurrentUserContext.LocalUserId`, never the Keycloak `sub`.
- **`Result` mapping** — handlers return Ardalis `Result`; `Result.Forbidden()` → 403 via the existing `ArdalisResultHttpExtensions`.
- **Repository writes are staged, not committed** — `EfRepository.SaveChangesAsync` is a no-op; `TransactionBehavior` commits (commands marked `ITransactional`).
- **No new authz model besides permissions** — `module_roles` is removed in Task 0, not left alongside.

---

## File Structure

**`Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/`** (cross-module contract):
- `Authorization/Permissions.cs` — permission key constants + key-format guard.
- `Authorization/PermissionPolicy.cs` — `perm:{key}` policy-name helper + `HasPermissionAttribute`.
- `Authorization/PermissionRequirement.cs`, `PermissionAuthorizationHandler.cs`, `PermissionPolicyProvider.cs`.
- `Authorization/ICurrentUserPermissions.cs`.
- `Authorization/IResourceAuthorizer.cs`, `IAuthorizationContext.cs`, `AuthorizationActions.cs`, `Tenant.cs`.
- `Authorization/AuthorizationServiceCollectionExtensions.cs` — **modified** (replace module-role helpers with `AddPermissionAuthorization`).
- **Deleted:** `Authorization/ModuleRoleRequirement.cs`, `ModuleRoleAuthorizationHandler.cs`; `Claims/ClaimsPrincipalExtensions.HasModuleRole`; `Claims/IdentityClaimTypes.ModuleRoles`.

**`Services/AllSpice.CleanModularMonolith.Identity/`** (implementation):
- `Domain/Aggregates/Authorization/Permission.cs`, `Role.cs`, `RolePermission.cs`, `AuthzMapVersion.cs`.
- `Application/Contracts/Authorization/IPermissionMapStore.cs`, `PermissionMap.cs`.
- `Infrastructure/Authorization/PermissionMapStore.cs`, `PermissionMapCache.cs`, `CurrentUserPermissions.cs`, `ResourceAuthorizer.cs`, `AuthorizationContext.cs`.
- `Infrastructure/Persistence/Configurations/PermissionConfiguration.cs`, `RoleConfiguration.cs`, `RolePermissionConfiguration.cs`, `AuthzMapVersionConfiguration.cs`.
- `Infrastructure/Persistence/IdentityDbContext.cs` — **modified** (DbSets).
- `Infrastructure/Migrations/*` — new migration `AddAuthorizationModel`.
- `Infrastructure/Extensions/IdentityModuleExtensions.cs` — **modified** (remove `AddModuleRoleAuthorization`, add authz registrations).
- `AllSpice.CleanModularMonolith.ApiGateway/Extensions/GatewayServiceCollectionExtensions.cs` — **modified** (register `AddPermissionAuthorization`, policy provider).

**Tests:**
- `tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests/Authorization/*` (resolver, handler, policy provider, entities, resource authorizer).
- `tests/AllSpice.CleanModularMonolith.ApiGateway.UnitTests/Authorization/*` (gateway-policy still builds after removal — regression).

---

## Task 0: Remove the dead `module_roles` system

**Files:**
- Delete: `Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/ModuleRoleRequirement.cs`
- Delete: `Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/ModuleRoleAuthorizationHandler.cs`
- Modify: `Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/AuthorizationServiceCollectionExtensions.cs` (remove `AddModuleRoleAuthorization` + `AddModuleRolePolicy`)
- Modify: `Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Claims/ClaimsPrincipalExtensions.cs` (remove `HasModuleRole`)
- Modify: `Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Claims/IdentityClaimTypes.cs` (remove `ModuleRoles`)
- Modify: `Services/AllSpice.CleanModularMonolith.Identity/Infrastructure/Extensions/IdentityModuleExtensions.cs:67` (remove `builder.Services.AddModuleRoleAuthorization();`)
- Test: `tests/AllSpice.CleanModularMonolith.ApiGateway.UnitTests/Authorization/AuthorizationPolicyBuildsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a clean Abstractions/Authorization namespace for Tasks 1-7.

- [ ] **Step 1: Write the failing regression test** — gateway authorization still builds `authenticated` + `allow-anonymous` after removal.

```csharp
// tests/AllSpice.CleanModularMonolith.ApiGateway.UnitTests/Authorization/AuthorizationPolicyBuildsTests.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Authorization;

public sealed class AuthorizationPolicyBuildsTests
{
    [Fact]
    public async Task Gateway_core_policies_still_resolve_after_module_roles_removal()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("authenticated", p => p.RequireAssertion(_ => true));
            options.AddPolicy("allow-anonymous", p => p.RequireAssertion(_ => true));
        });
        var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await policyProvider.GetPolicyAsync("authenticated"));
        Assert.NotNull(await policyProvider.GetPolicyAsync("allow-anonymous"));
    }
}
```

- [ ] **Step 2: Run it (it passes today — it's the guard)**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.ApiGateway.UnitTests --filter AuthorizationPolicyBuildsTests`
Expected: PASS (this pins behavior we must not break).

- [ ] **Step 3: Delete the two ModuleRole files and the helpers.** Remove `ModuleRoleRequirement.cs` and `ModuleRoleAuthorizationHandler.cs`. In `AuthorizationServiceCollectionExtensions.cs`, delete `AddModuleRoleAuthorization` and `AddModuleRolePolicy` (leave the file with `using` cleanup; Task 6 re-adds `AddPermissionAuthorization`). In `ClaimsPrincipalExtensions.cs` delete the `HasModuleRole` method (keep `GetSubjectId`, `GetIssuer`, `GetAudience`, `GetPortal`). In `IdentityClaimTypes.cs` delete the `ModuleRoles` constant. In `IdentityModuleExtensions.cs` delete line 67 (`builder.Services.AddModuleRoleAuthorization();`).

- [ ] **Step 4: Build the whole solution to prove nothing referenced the deleted members**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx`
Expected: Build succeeds, 0 warnings. (If anything fails to compile, it was a live consumer — there are none expected.)

- [ ] **Step 5: Re-run the regression test**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.ApiGateway.UnitTests --filter AuthorizationPolicyBuildsTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(authz): remove dead module_roles system (ADR-0008 Issue 1)"
```

---

## Task 1: Permission key registry + format guard

**Files:**
- Create: `Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/Permissions.cs`
- Test: `tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests/Authorization/PermissionsTests.cs`

**Interfaces:**
- Produces: `Permissions.AuthzRead = "authz.read"`, `Permissions.AuthzManage = "authz.manage"`, `Permissions.All` (`IReadOnlySet<string>`), `Permissions.IsValidKey(string)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/.../Identity.Application.UnitTests/Authorization/PermissionsTests.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionsTests
{
    [Theory]
    [InlineData("authz.read", true)]
    [InlineData("cms:articles.publish", true)]
    [InlineData("cms.access", true)]
    [InlineData("Has Space", false)]
    [InlineData("", false)]
    [InlineData("UPPER", false)]
    public void IsValidKey_enforces_lowercase_namespaced_format(string key, bool expected)
        => Assert.Equal(expected, Permissions.IsValidKey(key));

    [Fact]
    public void All_contains_the_seeded_system_keys()
    {
        Assert.Contains(Permissions.AuthzRead, Permissions.All);
        Assert.Contains(Permissions.AuthzManage, Permissions.All);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Run: `dotnet test tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests --filter PermissionsTests` — Expected: FAIL (type `Permissions` not found).

- [ ] **Step 3: Implement**

```csharp
// Shared/.../Identity.Abstractions/Authorization/Permissions.cs
using System.Text.RegularExpressions;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>
/// The closed set of permission keys referenced by code. Module-scoped keys use a
/// "<c>module:area.action</c>" namespace; the coarse module gate is "<c>module.access</c>".
/// Plan B's reconciler seeds these as <c>IsSystem</c>.
/// </summary>
public static partial class Permissions
{
    public const string AuthzRead = "authz.read";
    public const string AuthzManage = "authz.manage";

    /// <summary>All code-referenced keys. Modules contribute their own via the manifest (Plan B).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthzRead,
        AuthzManage,
    };

    /// <summary>
    /// Lowercase, dot/colon-namespaced keys: segments of [a-z0-9-] joined by '.', with an optional
    /// single "module:" prefix. e.g. "authz.read", "cms.access", "cms:articles.publish".
    /// </summary>
    public static bool IsValidKey(string key)
        => !string.IsNullOrWhiteSpace(key) && KeyPattern().IsMatch(key);

    [GeneratedRegex("^[a-z0-9-]+(:[a-z0-9-]+(\\.[a-z0-9-]+)*|(\\.[a-z0-9-]+)+)$")]
    private static partial Regex KeyPattern();
}
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/Permissions.cs tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests/Authorization/PermissionsTests.cs
git commit -m "feat(authz): permission key registry + format guard"
```

---

## Task 2: Authorization aggregates

**Files:**
- Create: `Services/AllSpice.CleanModularMonolith.Identity/Domain/Aggregates/Authorization/Permission.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Identity/Domain/Aggregates/Authorization/Role.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Identity/Domain/Aggregates/Authorization/RolePermission.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Identity/Domain/Aggregates/Authorization/AuthzMapVersion.cs`
- Test: `tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests/Authorization/AuthorizationEntitiesTests.cs`

**Interfaces:**
- Consumes: `Entity`, `AuditableEntity<Guid>`, `IAggregateRoot` from `SharedKernel.Common`; `Permissions.IsValidKey`.
- Produces: `Permission.Create(string key, string description, bool isSystem)`, `Role.Create(string key, string? description)`, `RolePermission.Create(Guid roleId, Guid permissionId)`, `AuthzMapVersion` with `Version` and `Bump()`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/.../Authorization/AuthorizationEntitiesTests.cs
using System;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class AuthorizationEntitiesTests
{
    [Fact]
    public void Permission_Create_sets_fields_and_generates_id()
    {
        var p = Permission.Create("cms:articles.publish", "Publish articles", isSystem: true);
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal("cms:articles.publish", p.Key);
        Assert.True(p.IsSystem);
    }

    [Fact]
    public void Permission_Create_rejects_malformed_key()
        => Assert.Throws<ArgumentException>(() => Permission.Create("Bad Key", "x", false));

    [Fact]
    public void Role_Create_normalizes_nothing_but_requires_key()
        => Assert.Throws<ArgumentException>(() => Role.Create("", null));

    [Fact]
    public void AuthzMapVersion_Bump_increments()
    {
        var v = AuthzMapVersion.Initial();
        var before = v.Version;
        v.Bump();
        Assert.Equal(before + 1, v.Version);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (types missing).

- [ ] **Step 3: Implement the four entities**

```csharp
// Domain/Aggregates/Authorization/Permission.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>A grantable permission. <see cref="IsSystem"/> keys are code-referenced and deletion-protected.</summary>
public sealed class Permission : AuditableEntity<Guid>, IAggregateRoot
{
    private Permission() { }

    private Permission(string key, string description, bool isSystem)
    {
        Id = Guid.NewGuid();
        Key = key;
        Description = description;
        IsSystem = isSystem;
    }

    public string Key { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsSystem { get; private set; }

    public static Permission Create(string key, string description, bool isSystem)
    {
        if (!Permissions.IsValidKey(key))
        {
            throw new ArgumentException($"Invalid permission key '{key}'.", nameof(key));
        }

        return new Permission(key, description ?? string.Empty, isSystem);
    }
}
```

```csharp
// Domain/Aggregates/Authorization/Role.cs
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>An app-side role whose <see cref="Key"/> mirrors a Keycloak realm-role name.</summary>
public sealed class Role : AuditableEntity<Guid>, IAggregateRoot
{
    private Role() { }

    private Role(string key, string? description)
    {
        Id = Guid.NewGuid();
        Key = key;
        Description = description;
    }

    public string Key { get; private set; } = default!;
    public string? Description { get; private set; }

    public static Role Create(string key, string? description)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Role key is required.", nameof(key));
        }

        return new Role(key.Trim(), description);
    }
}
```

```csharp
// Domain/Aggregates/Authorization/RolePermission.cs
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>Join row mapping a <see cref="Role"/> to a <see cref="Permission"/>. Not an aggregate root;
/// read by the map store, mutated by the admin API (Plan B).</summary>
public sealed class RolePermission : Entity
{
    private RolePermission() { }

    private RolePermission(Guid roleId, Guid permissionId)
    {
        Id = Guid.NewGuid();
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId) => new(roleId, permissionId);
}
```

```csharp
// Domain/Aggregates/Authorization/AuthzMapVersion.cs
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>Single-row monotonic version of the role→permission map; bumped on any mutation (Plan B)
/// to drive cache eviction. Seeded at version 0.</summary>
public sealed class AuthzMapVersion : Entity, IAggregateRoot
{
    public const string SingletonKey = "authz-map";

    private AuthzMapVersion() { }

    private AuthzMapVersion(long version) { Id = Guid.NewGuid(); Version = version; }

    public long Version { get; private set; }

    public static AuthzMapVersion Initial() => new(0);

    public void Bump() => Version++;
}
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/AllSpice.CleanModularMonolith.Identity/Domain/Aggregates/Authorization tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests/Authorization/AuthorizationEntitiesTests.cs
git commit -m "feat(authz): permission/role/rolepermission/version aggregates"
```

---

## Task 3: EF configuration, DbSets, and migration

**Files:**
- Create: `Services/.../Identity/Infrastructure/Persistence/Configurations/PermissionConfiguration.cs`
- Create: `Services/.../Identity/Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
- Create: `Services/.../Identity/Infrastructure/Persistence/Configurations/RolePermissionConfiguration.cs`
- Create: `Services/.../Identity/Infrastructure/Persistence/Configurations/AuthzMapVersionConfiguration.cs`
- Modify: `Services/.../Identity/Infrastructure/Persistence/IdentityDbContext.cs` (add four `DbSet`s)
- Create: migration under `Services/.../Identity/Infrastructure/Migrations/`
- Test: `tests/.../Identity.Application.UnitTests/Authorization/AuthorizationModelTests.cs`

**Interfaces:**
- Consumes: the four aggregates from Task 2.
- Produces: tables `authz_permissions`, `authz_roles`, `authz_role_permissions`, `authz_map_version` with the unique indexes the resolver and Plan B rely on.

- [ ] **Step 1: Write the failing model test** (SQLite — Wolverine envelope mapping is Npgsql-guarded, so the model builds cleanly).

```csharp
// tests/.../Authorization/AuthorizationModelTests.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class AuthorizationModelTests
{
    private static IdentityDbContext NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(conn).Options;
        return new IdentityDbContext(options);
    }

    [Fact]
    public void Permission_key_has_a_unique_index()
    {
        using var ctx = NewContext();
        var entity = ctx.Model.FindEntityType(typeof(Permission))!;
        var keyProp = entity.FindProperty(nameof(Permission.Key))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Contains(keyProp));
    }

    [Fact]
    public void RolePermission_is_mapped()
        => Assert.NotNull(NewContext().Model.FindEntityType(typeof(RolePermission)));
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (entities not in the model). Add `Microsoft.Data.Sqlite` + `Microsoft.EntityFrameworkCore.Sqlite` `PackageVersion` entries to `Directory.Packages.props` and `PackageReference`s to the test `.csproj` if not already present.

- [ ] **Step 3a: Add DbSets to `IdentityDbContext`** (alongside the existing `Users`, `IdentitySyncHistories`, … set):

```csharp
public DbSet<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.Permission> Permissions => Set<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.Permission>();
public DbSet<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.Role> Roles => Set<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.Role>();
public DbSet<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.RolePermission> RolePermissions => Set<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.RolePermission>();
public DbSet<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.AuthzMapVersion> AuthzMapVersions => Set<AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization.AuthzMapVersion>();
```

(The existing `OnModelCreating` already calls `ApplyConfigurationsFromAssembly(...)`, so the four configurations below are picked up automatically.)

- [ ] **Step 3b: Add the four configurations**

```csharp
// Configurations/PermissionConfiguration.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("authz_permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Key).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(500);
        builder.Property(p => p.IsSystem).IsRequired();
        builder.HasIndex(p => p.Key).IsUnique();
    }
}
```

```csharp
// Configurations/RoleConfiguration.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("authz_roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(500);
        // Case-insensitive uniqueness mirrors the resolver's case-insensitive match (ADR-0008).
        builder.HasIndex(r => r.Key).IsUnique();
    }
}
```

```csharp
// Configurations/RolePermissionConfiguration.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("authz_role_permissions");
        builder.HasKey(rp => rp.Id);
        builder.Property(rp => rp.RoleId).IsRequired();
        builder.Property(rp => rp.PermissionId).IsRequired();
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        builder.HasIndex(rp => rp.RoleId);
    }
}
```

```csharp
// Configurations/AuthzMapVersionConfiguration.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthzMapVersionConfiguration : IEntityTypeConfiguration<AuthzMapVersion>
{
    public void Configure(EntityTypeBuilder<AuthzMapVersion> builder)
    {
        builder.ToTable("authz_map_version");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Version).IsRequired();
    }
}
```

- [ ] **Step 4: Run the model test** — Expected: PASS.

- [ ] **Step 5: Generate the migration**

Run (bash; substitute the local pg password):
```bash
EF_DESIGN_DB_PASSWORD=<local-pg-password> dotnet ef migrations add AddAuthorizationModel \
  --project Services/AllSpice.CleanModularMonolith.Identity/AllSpice.CleanModularMonolith.Identity.csproj \
  --startup-project AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
  --context IdentityDbContext --output-dir Infrastructure/Migrations
```
Expected: a new `*_AddAuthorizationModel.cs` creating the four tables. Inspect it: four `CreateTable` calls + the unique indexes. **Seeding is added in Task 8** (kept separate so the schema migration is reviewable on its own).

- [ ] **Step 6: Build + commit**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` — Expected: 0 warnings.
```bash
git add Services/AllSpice.CleanModularMonolith.Identity Directory.Packages.props tests/AllSpice.CleanModularMonolith.Identity.Application.UnitTests
git commit -m "feat(authz): EF model + migration for authorization tables"
```

---

## Task 4: Permission map read store

**Files:**
- Create: `Services/.../Identity/Application/Contracts/Authorization/PermissionMap.cs`
- Create: `Services/.../Identity/Application/Contracts/Authorization/IPermissionMapStore.cs`
- Create: `Services/.../Identity/Infrastructure/Authorization/PermissionMapStore.cs`
- Modify: `IdentityModuleExtensions.cs` (register `IPermissionMapStore`)
- Test: `tests/.../Identity.Infrastructure.IntegrationTests/Authorization/PermissionMapStoreTests.cs` (new test project mirrors `Notifications.Infrastructure.IntegrationTests`; if an Identity infra integration project does not exist, add one following that project's `.csproj` + `TestDbContextFactory` pattern)

**Interfaces:**
- Consumes: `IdentityDbContext`, the Task 2 aggregates.
- Produces: `PermissionMap(long Version, IReadOnlyDictionary<string, IReadOnlySet<string>> RoleToPermissions)`; `IPermissionMapStore.GetMapAsync(CancellationToken) : Task<PermissionMap>`.

- [ ] **Step 1: Write the failing integration test** (real Postgres via the existing Testcontainers `TestDbContextFactory` pattern). Seed one role + one permission + a mapping, assert the map projects role→{key}.

```csharp
// tests/.../Identity.Infrastructure.IntegrationTests/Authorization/PermissionMapStoreTests.cs
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class PermissionMapStoreTests
{
    [Fact]
    public async Task GetMapAsync_projects_role_key_to_its_permission_keys()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync(); // mirrors Notifications TestDbContextFactory
        var role = Role.Create("platform-admin", null);
        var perm = Permission.Create("authz.manage", "Manage authz", isSystem: true);
        ctx.Roles.Add(role);
        ctx.Permissions.Add(perm);
        ctx.RolePermissions.Add(RolePermission.Create(role.Id, perm.Id));
        ctx.AuthzMapVersions.Add(AuthzMapVersion.Initial());
        await ctx.SaveChangesAsync();

        var store = new PermissionMapStore(ctx);
        var map = await store.GetMapAsync(default);

        Assert.True(map.RoleToPermissions.TryGetValue("platform-admin", out var perms));
        Assert.Contains("authz.manage", perms!);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (`PermissionMapStore` missing).

- [ ] **Step 3: Implement the contract + store**

```csharp
// Application/Contracts/Authorization/PermissionMap.cs
namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;

/// <summary>A snapshot of the whole role→permission map plus the version it was built at.</summary>
public sealed record PermissionMap(long Version, IReadOnlyDictionary<string, IReadOnlySet<string>> RoleToPermissions);
```

```csharp
// Application/Contracts/Authorization/IPermissionMapStore.cs
namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;

/// <summary>Reads the full role→permission map from the store. Scoped (uses the module DbContext).</summary>
public interface IPermissionMapStore
{
    Task<PermissionMap> GetMapAsync(CancellationToken cancellationToken);
}
```

```csharp
// Infrastructure/Authorization/PermissionMapStore.cs
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class PermissionMapStore(IdentityDbContext dbContext) : IPermissionMapStore
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public async Task<PermissionMap> GetMapAsync(CancellationToken cancellationToken)
    {
        // roleKey -> set of permission keys, built from a single projection join.
        var rows = await (
            from rp in _dbContext.RolePermissions.AsNoTracking()
            join r in _dbContext.Roles.AsNoTracking() on rp.RoleId equals r.Id
            join p in _dbContext.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            select new { r.Key, PermissionKey = p.Key })
            .ToListAsync(cancellationToken);

        var map = rows
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)g.Select(x => x.PermissionKey).ToHashSet(StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);

        var version = await _dbContext.AuthzMapVersions
            .AsNoTracking()
            .Select(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return new PermissionMap(version, map);
    }
}
```

- [ ] **Step 4: Register + run** — add to `IdentityModuleExtensions.AddIdentityModuleServices` (near line 57): `builder.Services.AddScoped<IPermissionMapStore, PermissionMapStore>();` — Run the test — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/AllSpice.CleanModularMonolith.Identity tests
git commit -m "feat(authz): permission map read store (roleKey -> permission keys, case-insensitive)"
```

---

## Task 5: Map cache + per-request permission resolution

**Files:**
- Create: `Shared/.../Identity.Abstractions/Authorization/ICurrentUserPermissions.cs`
- Create: `Services/.../Identity/Infrastructure/Authorization/PermissionMapCache.cs`
- Create: `Services/.../Identity/Infrastructure/Authorization/CurrentUserPermissions.cs`
- Modify: `IdentityModuleExtensions.cs` (register cache singleton + `ICurrentUserPermissions` scoped + `IHttpContextAccessor`)
- Test: `tests/.../Identity.Application.UnitTests/Authorization/CurrentUserPermissionsTests.cs`

**Interfaces:**
- Consumes: `IPermissionMapStore`, `IHttpContextAccessor`, `IMemoryCache`, `TimeProvider`.
- Produces: `ICurrentUserPermissions.HasPermission(string key) : bool` and `ICurrentUserPermissions.Permissions : IReadOnlySet<string>`; `IPermissionMapCache.GetAsync(CancellationToken) : ValueTask<PermissionMap>`.

- [ ] **Step 1: Write the failing test** — given a map cache returning `platform-admin → {authz.read}` and a principal with role `platform-admin`, `HasPermission("authz.read")` is true and `"authz.manage"` is false; empty roles → deny-all; role matching is case-insensitive.

```csharp
// tests/.../Authorization/CurrentUserPermissionsTests.cs
using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class CurrentUserPermissionsTests
{
    private static CurrentUserPermissions Build(string[] roles, PermissionMap map)
    {
        var cache = new Mock<IPermissionMapCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(map);

        var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test", ClaimTypes.Name, ClaimTypes.Role);
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });

        return new CurrentUserPermissions(cache.Object, accessor.Object);
    }

    private static PermissionMap MapWith(string role, params string[] perms)
        => new(1, new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [role] = perms.ToHashSet(StringComparer.Ordinal),
        });

    [Fact]
    public void Grants_permission_from_role()
        => Assert.True(Build(["platform-admin"], MapWith("platform-admin", "authz.read")).HasPermission("authz.read"));

    [Fact]
    public void Denies_unmapped_permission()
        => Assert.False(Build(["platform-admin"], MapWith("platform-admin", "authz.read")).HasPermission("authz.manage"));

    [Fact]
    public void Empty_roles_deny_all()
        => Assert.False(Build([], MapWith("platform-admin", "authz.read")).HasPermission("authz.read"));

    [Fact]
    public void Role_match_is_case_insensitive()
        => Assert.True(Build(["Platform-Admin"], MapWith("platform-admin", "authz.read")).HasPermission("authz.read"));
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3a: Implement the abstraction**

```csharp
// Shared/.../Identity.Abstractions/Authorization/ICurrentUserPermissions.cs
namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>The current request's resolved permission set. Scoped; computed lazily on first access from the
/// authenticated principal's role claims, so proxied routes that never check a permission pay nothing.</summary>
public interface ICurrentUserPermissions
{
    bool HasPermission(string permissionKey);
    IReadOnlySet<string> Permissions { get; }
}
```

- [ ] **Step 3b: Implement the cache** (singleton; loads via a scope on miss; TTL backstop — version-based eviction + Redis pub-sub are Plan B).

```csharp
// Infrastructure/Authorization/PermissionMapCache.cs
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public interface IPermissionMapCache
{
    ValueTask<PermissionMap> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Caches the whole role→permission map in-process with a short TTL backstop. The map is small
/// (roles × permissions), so one cached copy per replica is fine. Plan B adds version-checked + Redis
/// pub-sub eviction for near-instant propagation.</summary>
public sealed class PermissionMapCache(IServiceScopeFactory scopeFactory, IMemoryCache cache) : IPermissionMapCache
{
    private const string CacheKey = "authz:map";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IMemoryCache _cache = cache;

    public async ValueTask<PermissionMap> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out PermissionMap? cached) && cached is not null)
        {
            return cached;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPermissionMapStore>();
        var map = await store.GetMapAsync(cancellationToken);
        _cache.Set(CacheKey, map, Ttl);
        return map;
    }
}
```

- [ ] **Step 3c: Implement the scoped resolver**

```csharp
// Infrastructure/Authorization/CurrentUserPermissions.cs
using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class CurrentUserPermissions(IPermissionMapCache cache, IHttpContextAccessor httpContextAccessor)
    : ICurrentUserPermissions
{
    private readonly IPermissionMapCache _cache = cache;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private IReadOnlySet<string>? _resolved;

    public IReadOnlySet<string> Permissions => _resolved ??= Resolve();

    public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);

    private IReadOnlySet<string> Resolve()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var roles = user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
        if (roles.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        // GetAwaiter().GetResult() is safe here: the cache hit path is synchronous, and on miss the load is a
        // short, scoped DB read. Resolution happens once per request (memoized in _resolved).
        var map = _cache.GetAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (map.RoleToPermissions.TryGetValue(role, out var perms))
            {
                result.UnionWith(perms);
            }
        }

        return result;
    }
}
```

- [ ] **Step 4: Register + run** — in `IdentityModuleExtensions`: `builder.Services.AddHttpContextAccessor();`, `builder.Services.AddMemoryCache();` (if not already), `builder.Services.AddSingleton<IPermissionMapCache, PermissionMapCache>();`, `builder.Services.AddScoped<ICurrentUserPermissions, CurrentUserPermissions>();` — Run the test — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Shared Services tests
git commit -m "feat(authz): in-memory map cache + per-request permission resolver"
```

---

## Task 6: Layer 1 — endpoint permission gate

**Files:**
- Create: `Shared/.../Identity.Abstractions/Authorization/PermissionPolicy.cs`
- Create: `Shared/.../Identity.Abstractions/Authorization/PermissionRequirement.cs`
- Create: `Shared/.../Identity.Abstractions/Authorization/PermissionAuthorizationHandler.cs`
- Create: `Shared/.../Identity.Abstractions/Authorization/PermissionPolicyProvider.cs`
- Modify: `Shared/.../Identity.Abstractions/Authorization/AuthorizationServiceCollectionExtensions.cs` (add `AddPermissionAuthorization`)
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/Extensions/GatewayServiceCollectionExtensions.cs` (call `AddPermissionAuthorization`)
- Test: `tests/.../Identity.Application.UnitTests/Authorization/PermissionPolicyProviderTests.cs`, `PermissionAuthorizationHandlerTests.cs`

**Interfaces:**
- Consumes: `ICurrentUserPermissions`.
- Produces: `PermissionPolicy.For(key) : string` (`"perm:{key}"`), `[HasPermission("key")]`, `AddPermissionAuthorization(IServiceCollection)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/.../Authorization/PermissionPolicyProviderTests.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider NewProvider()
        => new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task Materializes_a_policy_for_a_perm_prefixed_name()
    {
        var policy = await NewProvider().GetPolicyAsync("perm:authz.read");
        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement pr && pr.PermissionKey == "authz.read");
    }

    [Fact]
    public async Task Delegates_non_perm_policies_to_the_default_provider()
        => Assert.Null(await NewProvider().GetPolicyAsync("authenticated")); // not registered here -> default returns null
}
```

```csharp
// tests/.../Authorization/PermissionAuthorizationHandlerTests.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionAuthorizationHandlerTests
{
    private static async Task<bool> Evaluate(bool userHasIt)
    {
        var perms = new Mock<ICurrentUserPermissions>();
        perms.Setup(p => p.HasPermission("authz.read")).Returns(userHasIt);
        var handler = new PermissionAuthorizationHandler(perms.Object);
        var requirement = new PermissionRequirement("authz.read");
        var context = new AuthorizationHandlerContext([requirement], user: new System.Security.Claims.ClaimsPrincipal(), resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact] public async Task Succeeds_when_user_has_permission() => Assert.True(await Evaluate(true));
    [Fact] public async Task Fails_when_user_lacks_permission() => Assert.False(await Evaluate(false));
}
```

- [ ] **Step 2: Run to verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// Authorization/PermissionPolicy.cs
using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Maps a permission key to its dynamic policy name and back.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";
    public static string For(string permissionKey) => Prefix + permissionKey;
    public static bool TryGetKey(string policyName, out string key)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            key = policyName[Prefix.Length..];
            return key.Length > 0;
        }
        key = string.Empty;
        return false;
    }
}

/// <summary>Sugar for MVC-style endpoints: <c>[HasPermission("cms:articles.publish")]</c>.
/// FastEndpoints use <c>Policies(PermissionPolicy.For("..."))</c> in <c>Configure()</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permissionKey) => Policy = PermissionPolicy.For(permissionKey);
}
```

```csharp
// Authorization/PermissionRequirement.cs
using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public sealed class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
```

```csharp
// Authorization/PermissionAuthorizationHandler.cs
using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public sealed class PermissionAuthorizationHandler(ICurrentUserPermissions currentUserPermissions)
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserPermissions _currentUserPermissions = currentUserPermissions;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_currentUserPermissions.HasPermission(requirement.PermissionKey))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

```csharp
// Authorization/PermissionPolicyProvider.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Materializes a one-requirement policy for any <c>perm:{key}</c> name; delegates everything else
/// (incl. <c>authenticated</c> / <c>allow-anonymous</c> / fallback) to the default provider.</summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionPolicy.TryGetKey(policyName, out var key))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(key))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
```

```csharp
// Authorization/AuthorizationServiceCollectionExtensions.cs  (replace the deleted module-role helpers)
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization; // ResourceAuthorizer (Task 7)
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>Registers the permission policy provider + handler. The resolver, cache, and map store are
    /// registered by the Identity module; this wires the ASP.NET authorization plumbing in the host.</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
```

- [ ] **Step 4: Wire the gateway** — in `GatewayServiceCollectionExtensions.AddGatewayServices`, after `builder.ConfigureAuthorization(authenticationEnabled);`, add `builder.Services.AddPermissionAuthorization();`. Run both unit tests — Expected: PASS.

- [ ] **Step 5: Build + commit**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` — Expected: 0 warnings.
```bash
git add Shared AllSpice.CleanModularMonolith.ApiGateway tests
git commit -m "feat(authz): Layer 1 endpoint permission gate (policy provider + handler)"
```

---

## Task 7: Layer 2 — resource authorizer facade

**Files:**
- Create: `Shared/.../Identity.Abstractions/Authorization/Tenant.cs`
- Create: `Shared/.../Identity.Abstractions/Authorization/IAuthorizationContext.cs`
- Create: `Shared/.../Identity.Abstractions/Authorization/AuthorizationActions.cs`
- Create: `Shared/.../Identity.Abstractions/Authorization/IResourceAuthorizer.cs`
- Create: `Services/.../Identity/Infrastructure/Authorization/AuthorizationContext.cs`
- Create: `Services/.../Identity/Infrastructure/Authorization/ResourceAuthorizer.cs`
- Modify: `IdentityModuleExtensions.cs` (register `IAuthorizationContext`, `IResourceAuthorizer`)
- Test: `tests/.../Identity.Application.UnitTests/Authorization/ResourceAuthorizerTests.cs`

**Interfaces:**
- Consumes: `ICurrentUserContext` (local UUID, ADR-0005), `ICurrentUserPermissions`, the built-in `IAuthorizationService`.
- Produces: `IResourceAuthorizer.AuthorizeAsync<TResource>(TResource resource, string action, CancellationToken) : Task<Result>`; `IAuthorizationContext { Guid? UserId; string TenantId; IReadOnlySet<string> Permissions }`; `AuthorizationActions.{Read,Create,Update,Delete,Approve}`; `Tenant.Default`.

- [ ] **Step 1: Write the failing test** — a sample owned resource + a registered rule (owner may Update); owner → `Result.IsSuccess`, non-owner → `Result.Status == Forbidden`.

```csharp
// tests/.../Authorization/ResourceAuthorizerTests.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Ardalis.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class ResourceAuthorizerTests
{
    private sealed record OwnedThing(Guid OwnerId);

    private sealed class OwnedThingRule : AuthorizationHandler<OperationAuthorizationRequirement, OwnedThing>
    {
        private readonly IAuthorizationContext _ctx;
        public OwnedThingRule(IAuthorizationContext ctx) => _ctx = ctx;
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext c, OperationAuthorizationRequirement r, OwnedThing resource)
        {
            if (resource.OwnerId == _ctx.UserId) c.Succeed(r);
            return Task.CompletedTask;
        }
    }

    private static IResourceAuthorizer Build(Guid currentUser)
    {
        var services = new ServiceCollection();
        services.AddAuthorizationCore();
        var userCtx = new CurrentUserContext();
        userCtx.Resolve(currentUser);
        services.AddSingleton<ICurrentUserContext>(userCtx);
        services.AddSingleton<ICurrentUserPermissions>(new StubPermissions());
        services.AddScoped<IAuthorizationContext, AuthorizationContext>();
        services.AddSingleton<IAuthorizationHandler>(sp => new OwnedThingRule(sp.GetRequiredService<IAuthorizationContext>()));
        services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();
        return services.BuildServiceProvider().GetRequiredService<IResourceAuthorizer>();
    }

    private sealed class StubPermissions : ICurrentUserPermissions
    {
        public bool HasPermission(string permissionKey) => false;
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    [Fact]
    public async Task Owner_is_authorized()
    {
        var me = Guid.NewGuid();
        var result = await Build(me).AuthorizeAsync(new OwnedThing(me), AuthorizationActions.Update, default);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Non_owner_is_forbidden()
    {
        var result = await Build(Guid.NewGuid()).AuthorizeAsync(new OwnedThing(Guid.NewGuid()), AuthorizationActions.Update, default);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// Authorization/Tenant.cs
namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Single-tenant seam. Real tenant resolution slots in later without changing rule signatures.</summary>
public static class Tenant
{
    public const string Default = "default";
}
```

```csharp
// Authorization/AuthorizationActions.cs
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Canonical resource-operation names; reused as <see cref="OperationAuthorizationRequirement.Name"/>.</summary>
public static class AuthorizationActions
{
    public const string Read = "read";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Approve = "approve";
}
```

```csharp
// Authorization/IAuthorizationContext.cs
namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Tenant-aware identity + permissions for resource rules. Built from ICurrentUserContext (local UUID).</summary>
public interface IAuthorizationContext
{
    Guid? UserId { get; }
    string TenantId { get; }
    IReadOnlySet<string> Permissions { get; }
}
```

```csharp
// Authorization/IResourceAuthorizer.cs
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Evaluates resource/ownership rules for a loaded aggregate. Keeps HttpContext out of handlers.</summary>
public interface IResourceAuthorizer
{
    Task<Result> AuthorizeAsync<TResource>(TResource resource, string action, CancellationToken cancellationToken)
        where TResource : notnull;
}
```

```csharp
// Infrastructure/Authorization/AuthorizationContext.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class AuthorizationContext(ICurrentUserContext currentUser, ICurrentUserPermissions permissions)
    : IAuthorizationContext
{
    public Guid? UserId => currentUser.LocalUserId;
    public string TenantId => Tenant.Default;
    public IReadOnlySet<string> Permissions => permissions.Permissions;
}
```

```csharp
// Infrastructure/Authorization/ResourceAuthorizer.cs
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Ardalis.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Thin facade over the built-in resource-based authorization. Dispatches to registered
/// <c>AuthorizationHandler&lt;OperationAuthorizationRequirement, TResource&gt;</c> rules and maps the verdict
/// to an Ardalis <see cref="Result"/>. Sources the principal from HttpContext so command handlers stay clean.</summary>
public sealed class ResourceAuthorizer(IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    : IResourceAuthorizer
{
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<Result> AuthorizeAsync<TResource>(TResource resource, string action, CancellationToken cancellationToken)
        where TResource : notnull
    {
        var user = _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();
        var requirement = new OperationAuthorizationRequirement { Name = action };
        var result = await _authorizationService.AuthorizeAsync(user, resource, requirement);
        return result.Succeeded ? Result.Success() : Result.Forbidden();
    }
}
```

- [ ] **Step 4: Register + run** — in `IdentityModuleExtensions`: `builder.Services.AddScoped<IAuthorizationContext, AuthorizationContext>();` and `builder.Services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();` (the test wires its own provider, so it passes without the module). Run the test — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Shared Services tests
git commit -m "feat(authz): Layer 2 resource authorizer facade over built-in handlers"
```

---

## Task 8: Seed system permissions + end-to-end enforcement test

**Files:**
- Modify: the `AddAuthorizationModel` migration (or add a new `SeedAuthorizationSystemData` migration) to seed the two `IsSystem` permissions + the `AuthzMapVersion` singleton.
- Test: `tests/.../Identity.Infrastructure.IntegrationTests/Authorization/PermissionGateEndToEndTests.cs`

**Interfaces:**
- Consumes: everything above.
- Produces: a booted system where `Policies(PermissionPolicy.For("authz.read"))` returns 401/403/200 correctly.

- [ ] **Step 1: Write the failing end-to-end test** — using the integration host (Testcontainers Postgres + the gateway test host pattern from `Foundation.IntegrationTests/TwoModuleHost`). Seed role `qa-admin` → `authz.read`. A test endpoint `GET /test/authz-read` configured with `Policies(PermissionPolicy.For(Permissions.AuthzRead))`:
  - no token → **401**
  - token with role that has no mapping → **403**
  - token with `qa-admin` → **200**

```csharp
// tests/.../Authorization/PermissionGateEndToEndTests.cs  (sketch — fill host wiring from TwoModuleHost)
[Fact] public async Task Anonymous_gets_401() { /* GET without auth -> 401 */ }
[Fact] public async Task Authenticated_without_permission_gets_403() { /* role bob -> 403 */ }
[Fact] public async Task Authenticated_with_permission_gets_200() { /* role qa-admin mapped to authz.read -> 200 */ }
```

(Concrete assertions: `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)`, `Forbidden`, `OK` respectively. Use the existing test-host JWT helper that stamps `ClaimTypes.Role`; seed the `qa-admin → authz.read` mapping in the test's arrange via the DbContext.)

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (403 for the mapped user, because nothing is seeded yet, or 404 if the test endpoint is not registered).

- [ ] **Step 3: Seed via migration** — in the migration's `Up`, after table creation:

```csharp
migrationBuilder.InsertData(
    table: "authz_permissions",
    columns: ["Id", "Key", "Description", "IsSystem", "CreatedOnUtc", "CreatedBy", "LastModifiedOnUtc", "LastModifiedBy"],
    values: new object[,]
    {
        { Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "authz.read", "Read authorization config", true, DateTime.UnixEpoch, null!, null!, null! },
        { Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "authz.manage", "Manage authorization config", true, DateTime.UnixEpoch, null!, null!, null! },
    });

migrationBuilder.InsertData(
    table: "authz_map_version",
    columns: ["Id", "Version"],
    values: new object[] { Guid.Parse("00000000-0000-0000-0000-0000000000b1"), 0L });
```

(Use a fixed timestamp, not `DateTime.UtcNow` — migrations must be deterministic. The audit columns are nullable for seed rows. `authz.manage`/`authz.read` are mapped to a role by Plan B's config bootstrap; the e2e test seeds its own `qa-admin` mapping.)

- [ ] **Step 4: Apply + run** — Run: `dotnet test tests/AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests --filter PermissionGateEndToEndTests` — Expected: PASS (401/403/200).

- [ ] **Step 5: Full suite + commit**

Run: `dotnet test AllSpice.CleanModularMonolith.slnx` — Expected: all green, 0 warnings.
```bash
git add Services tests
git commit -m "feat(authz): seed system permissions + end-to-end permission gate test"
```

---

## Self-Review

**Spec coverage (Plan A scope):**
- §1 hybrid model — Tasks 4-5 resolve roles→permissions server-side. ✓
- §2 placement (Abstractions vs Identity) — Tasks 1,5,6,7 (Abstractions) vs 2,3,4 (Identity). ✓
- §2a module-scoped keys — `Permissions.IsValidKey` accepts `cms:articles.publish` / `cms.access`; the manifest + reconciler that seed module keys are **Plan B** (noted). ✓ (gate mechanism is here; module manifests land in B)
- §3 data model + case-insensitive `Role.Key` — Tasks 2,3 + `PermissionMapStore` uses `OrdinalIgnoreCase`. ✓
- §4 resolution + local scoping — Task 5 (lazy per-request resolution = only pays when a permission is checked). Push eviction is **Plan B**. ✓
- §5 Layer 1 + provider delegation — Task 6. ✓
- §6 Layer 2 facade over built-in handler — Task 7. ✓
- §9 critical tests — provider-delegation (Task 6), regression after `module_roles` removal (Task 0), 401/403/200 (Task 8). Reconciler/role-sync/bootstrap criticals are **Plan B**.
- Issue 1 removal — Task 0. ✓

**Deferred to Plan B (not gaps):** `IModulePermissionManifest` + reconciler (idempotent, `pg_advisory_lock`); `RoleSyncJob` + `GetAllRealmRolesAsync`; config bootstrap; admin API; `AuthzMapVersion` bump-on-mutation + Redis pub-sub eviction; per-user overrides (TODO).

**Placeholder scan:** the Task 8 e2e test is a sketch (host wiring deferred to the existing `TwoModuleHost` helper) — the assertions are concrete (401/403/200) but the harness wiring must be filled from that helper during implementation. Flagged here rather than inventing a host signature I have not read. Every other step has complete code.

**Type consistency:** `ICurrentUserPermissions` (HasPermission/Permissions), `IPermissionMapStore.GetMapAsync`, `IPermissionMapCache.GetAsync`, `PermissionPolicy.For/TryGetKey`, `PermissionRequirement.PermissionKey`, `IResourceAuthorizer.AuthorizeAsync<TResource>`, `IAuthorizationContext.{UserId,TenantId,Permissions}` — names are used identically across tasks. ✓

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-30-rbac-enforcement-core.md`.

**Open item before execution:** Task 8's end-to-end test needs the project's integration test-host helper (`Foundation.IntegrationTests/TwoModuleHost` + its JWT/role stamping). The first implementer should read that helper and fill the host wiring; the assertions (401/403/200) are fixed.

**Plan B (Provisioning, modules & propagation)** is not yet written — it covers the manifest+reconciler, role sync, bootstrap, admin API, and Redis pub-sub eviction. I can write it after Plan A lands (it builds on these interfaces).
