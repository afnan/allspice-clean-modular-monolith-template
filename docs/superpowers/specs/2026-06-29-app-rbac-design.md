# In-App RBAC — Design

**Date:** 2026-06-29 (revised 2026-06-30 after eng review)
**Status:** Approved (design); pending implementation plan
**ADR:** [0008](../../adr/0008-in-app-permission-based-authorization.md)
**Scope:** Authorization (authz) inside the app. Authentication (authn) is unchanged — Keycloak remains the IdP and the source of user→role assignment.

---

## 0. Revision log — eng review (2026-06-30)

Seven decisions from `/plan-eng-review` are folded into the sections below:

1. **Remove the dead `module_roles` system** — permissions become the single authz model (prerequisite step).
2. **`Role` rows synced from Keycloak** — new `KeycloakRoleClient.GetAllRealmRolesAsync` + `RoleSyncJob`; degrades when Keycloak is unconfigured.
3. **Config-driven first-admin bootstrap** — `Authorization:BootstrapAdminRole` auto-granted `authz.manage`/`authz.read`, idempotent.
4. **Two-tier module gating** — coarse `module.access` + fine `module:action`, each module declares an `IModulePermissionManifest`.
5. **Resource authz = thin facade over the built-in handler** — `IResourceAuthorizer` wraps `AuthorizationHandler<TRequirement, TResource>`; handlers stay HttpContext-free.
6. **Resolver scoped to local endpoints** — not the shared pipeline; proxied routes don't pay.
7. **Push-based cache eviction** — durable `AuthzMapVersion` bump in the mutation tx + best-effort Redis pub-sub nudge → every replica evicts; 60s backstop TTL; in-process fallback when Redis is absent.

---

## 1. Problem & decisions

Keycloak authenticates users and assigns them **roles** (carried in the JWT via `realm_access.roles`). It cannot enforce application authorization: role-on-endpoint gates, module-level access, and per-entity resource rules that depend on domain state Keycloak never sees. This design adds a two-layer, permission-based authorization model owned by the app.

| Decision | Choice |
| --- | --- |
| **Granularity** | Permission-based **plus** resource/ownership rules. |
| **Source of truth** | **Hybrid.** Keycloak assigns *roles* (reuse `realm_access`). The app owns the permission catalog, the role→permission mapping, and resource rules. The app never stores user→role. |
| **Module gating** | Modules are permission namespaces with a **two-tier** gate: coarse `module.access` + fine `module:action`. Each module self-declares via `IModulePermissionManifest`. |
| **Tenancy** | Single-tenant today; the authorization seam is **tenant-aware** (`TenantId = Tenant.Default`; real resolution slots in later without changing rule signatures). |
| **Management** | **Fully dynamic** — permissions and mappings are admin-editable at runtime, with an `IsSystem` valve preventing silent lockout. |
| **Enforcement** | **Hybrid:** declarative `[HasPermission]` gate at the endpoint **+** `IResourceAuthorizer` (thin facade over the built-in handler) inside handlers. |

### Key properties

- **No re-login on permission change.** Permissions resolve server-side from the DB per request, never baked into the token. Edits propagate near-instantly via push eviction.
- **Modules depend only on `Identity.Abstractions`** for authz — never on Identity infrastructure.
- **Local UUID is the principal** for resource checks (ADR-0005); the Keycloak external id stays at the JWT boundary only.

---

## 2. Placement

Contract in `Identity.Abstractions`, implementation in the `Identity` module.

**`Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/`** (cross-module contract):
- `HasPermissionAttribute` — `[HasPermission("cms:articles.publish")]`.
- `PermissionRequirement` + `PermissionAuthorizationHandler`.
- `PermissionPolicyProvider` — dynamic `IAuthorizationPolicyProvider` materializing `perm:{key}` policies, **delegating to the default provider** for `authenticated` / `allow-anonymous` / fallback.
- `ICurrentUserPermissions` — resolved permission set for the current request.
- `IResourceAuthorizer`, `IAuthorizationContext`, `AuthorizationActions` (the facade; rules use the built-in `AuthorizationHandler<TRequirement, TResource>`).
- `IModulePermissionManifest` — a module declares its `module.access` key + `module:action` keys.
- `Permissions` — registry of code-referenced permission key constants.

**`Services/AllSpice.CleanModularMonolith.Identity/`** (owner + implementation):
- Domain: `Permission`, `Role`, `RolePermission` aggregates.
- Application/Infrastructure: bespoke `IPermissionRepository` / `IRolePermissionRepository` (+ impls, ADR-0004), `PermissionResolver` (+ caching + Redis pub-sub eviction), `RoleSyncJob`, the startup reconciler + bootstrap.
- Api: admin endpoints under `Api/Endpoints/Authorization/`.
- EF config + migration.

**Prerequisite (Issue 1):** delete `Identity.Abstractions/Authorization/ModuleRole*.cs`, `HasModuleRole`, `IdentityClaimTypes.ModuleRoles`, and the `AddModuleRoleAuthorization()` call (`IdentityModuleExtensions.cs:67`). A regression test confirms gateway auth (`authenticated` / `allow-anonymous`) still resolves after removal.

---

## 2a. Module-scoped permissions

Modules are permission namespaces. A module is gated in two tiers:

```
Module endpoint group:  [HasPermission("cms.access")]      <- coarse: enter the module at all
  endpoint:             [HasPermission("cms:articles.publish")]
  endpoint:             [HasPermission("cms:media.upload")]
```

Each module ships an `IModulePermissionManifest` declaring `cms.access` and its `cms:*` action keys. The reconciler scans all manifests at startup and seeds their keys as `IsSystem`. Adding a module therefore auto-registers its permissions; an admin maps them onto roles at runtime.

---

## 3. Data model

```
Permission      ( Id, Key UNIQUE, Description, IsSystem, <audit> )
Role            ( Id, Key UNIQUE  -- mirrors Keycloak realm-role name, Description, <audit> )
RolePermission  ( RoleId FK, PermissionId FK )      -- the admin-editable mapping
AuthzMapVersion ( Id = 1, Version )                 -- bumped in the mutation tx; drives cache eviction
```

- **User→Role assignment is NOT stored here** — it lives in Keycloak. `Role.Key` mirrors the realm-role *name*, the join key from the token into the app model. Matching is **case-insensitive + trimmed** (mirror the existing `HasModuleRole` `OrdinalIgnoreCase`) to avoid silent permission loss.
- **`Role` rows are synced from Keycloak** (Issue 2): a new `KeycloakRoleClient.GetAllRealmRolesAsync` + a `RoleSyncJob` (mirroring `KeycloakUserSyncJob`) upserts rows. The job **degrades gracefully when Keycloak is unconfigured** (app boots, `/health` Degraded).
- **`IsSystem`** protects code-referenced permission keys from admin deletion — admins still freely create/map non-system permissions; silent lockout is prevented.

---

## 4. Resolution, caching & propagation

A scoped `ICurrentUserPermissions` is resolved once per request, **only on local FastEndpoints** (a global pre-processor), so YARP-proxied routes don't pay (Issue 6):

```
realm_access role names ──► RolePermissionMap (in-memory) ──► HashSet<string> permission keys
```

- `PermissionResolver` reads the principal's role names (already on the token after the existing realm-role claim mapping) and projects the user's permission set from the cached map.
- **Propagation (Issue 7).** A mapping mutation bumps `AuthzMapVersion` in the **same transaction** (durable truth) and, after commit, publishes a **best-effort Redis pub-sub** message on an `authz:invalidate` channel. Every replica subscribes and **evicts its in-memory map** on receipt. A **60s backstop TTL** covers a missed message; when Redis is absent (single-node minimal run) eviction is in-process. Net: near-instant propagation, no per-request DB read, no re-login.

---

## 5. Enforcement — Layer 1 (coarse gate at the endpoint)

- `PermissionPolicyProvider` materializes a `perm:{key}` policy on demand and delegates to the default provider for existing policies.
- `PermissionAuthorizationHandler` succeeds iff `ICurrentUserPermissions` contains the key.
- Usage via FastEndpoints: `Configure()` → `Policies("perm:cms:articles.publish")`, with `[HasPermission]` as thin sugar. Fast **403** before the handler; visible in OpenAPI.
- Unauthenticated still → **401** via the gateway fallback policy.

---

## 6. Enforcement — Layer 2 (resource/ownership inside the handler)

- After the aggregate is loaded, the handler calls `await _authorizer.AuthorizeAsync(invoice, AuthorizationActions.Approve, ct)`.
- `IResourceAuthorizer` is a **thin facade over the built-in `AuthorizationHandler<TRequirement, TResource>`** (Issue 5): it feeds a tenant-aware `IAuthorizationContext { UserId, TenantId, Permissions }` and dispatches to the framework's rule engine. No custom dispatch loop; **no HttpContext in handlers**.
  - `UserId` = canonical **local UUID** from `ICurrentUserContext` (ADR-0005).
  - `TenantId` = `Tenant.Default` for now (seam).
- Rules express ownership / status / (future) tenant, e.g. `invoice.OwnerId == ctx.UserId && invoice.Status == Submitted`.
- On failure → `Result.Forbidden()` → **403** via the existing `ArdalisResultHttpExtensions` mapping.

**Why both layers:** Layer 1 answers "may this *kind* of user call this at all?" cheaply at the edge; Layer 2 answers "may *this* user act on *this specific* entity?" — which needs the loaded aggregate.

---

## 7. Admin API (runtime editability)

Under `Identity/Api/Endpoints/Authorization/`:

| Endpoint | Purpose | Gated by |
| --- | --- | --- |
| `GET /authz/permissions` | list catalog | `authz.read` |
| `POST /authz/permissions` · `DELETE …/{id}` | create / delete **non-system** permission | `authz.manage` |
| `GET /authz/roles` · `GET …/{key}/permissions` | view roles + mappings | `authz.read` |
| `PUT /authz/roles/{key}/permissions` | set a role's permission set | `authz.manage` |

- Endpoints are self-gated by `authz.*` permissions (seeded `IsSystem`).
- Every mutation is **audit-stamped** and **bumps `AuthzMapVersion`** in the same transaction; commit triggers the pub-sub eviction (§4).
- `DELETE` on an `IsSystem` permission → `Result.Forbidden`.
- **First-admin bootstrap (Issue 3):** `Authorization:BootstrapAdminRole` (config) names a realm role the reconciler auto-grants `authz.manage` + `authz.read` on startup, idempotently. Everything else stays dynamic. A warning is logged if the bootstrap role has no synced Keycloak counterpart (else the grant is inert → silent lockout).

---

## 8. Startup reconciler (drift + seeding)

An `IHostedService` / startup step in the Identity module, **idempotent and guarded by `pg_advisory_lock`** (reuse the `MigrationRunner` pattern, so concurrent replicas don't crash on the unique constraint):

1. Collect code-referenced keys from all `IModulePermissionManifest`s and `[HasPermission]` usages (cross-checked against the `Permissions` registry).
2. **Seed missing** keys as `IsSystem = true` (a freshly deployed enforced permission always exists).
3. **Log a warning** for each DB permission no code path references (candidate dead permission) — surfaced, never auto-deleted.
4. Apply the config-driven bootstrap mapping (§7).

Backed by an **architecture-fitness test** (ADR-0007): every `[HasPermission("k")]` literal must resolve to a known `Permissions` constant.

---

## 9. Testing strategy

22 planned paths (see the eng-review coverage diagram). Highlights, with the 5 critical ones called out:

- **Resolver:** projection; **empty roles → deny-all**; **unmapped role ignored**; **case-insensitive `Role.Key`**; backstop-TTL refresh; **pub-sub eviction evicts the map**; Redis-absent → in-process eviction; best-effort publish failure doesn't fail the mutation.
- **Layer 1:** policy materialization; **[CRIT] provider delegates to default for existing policies**; 401/403/200; **module `cms.access` group gate (→E2E)**.
- **Layer 2:** ownership 403; status rule; tenant-default seam.
- **Provisioning:** role sync upsert; **[CRIT] Keycloak-unconfigured → degrade, app boots, `/health` Degraded**; manifest+`[HasPermission]` keys seeded `IsSystem`; admin-deleted `IsSystem` re-seeded; **[CRIT] concurrent-replica seed idempotent (`pg_advisory_lock`)**; arch-test key-exists; **[CRIT] bootstrap role auto-granted, idempotent, no-config = no-op**.
- **Admin API:** mapping edit → version bump → next resolve sees change; `DELETE IsSystem` → Forbidden; endpoints self-gated.
- **[CRIT] Regression:** gateway auth (`authenticated` / `allow-anonymous`) still resolves after `module_roles` removal (mandatory, iron rule).

---

## 10. Out of scope

- **Admin UI screens** — Nuxt concern (Blazor removed).
- **Real multi-tenant resolution** — seam only (`Tenant.Default`).
- **Per-user direct permission grants/denies** — deferred (`TODOS.md`).
- **Authz-change audit view** — rows are audit-stamped; no query surface yet (`TODOS.md`).
- **Keycloak Authorization Services / UMA** — authz is deliberately owned in-app.
