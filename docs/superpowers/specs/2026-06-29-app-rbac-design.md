# In-App RBAC — Design

**Date:** 2026-06-29
**Status:** Approved (design); pending implementation plan
**Scope:** Authorization (authz) inside the app. Authentication (authn) is unchanged — Keycloak remains the IdP and the source of user→role assignment.

---

## 1. Problem & decisions

Keycloak authenticates users and assigns them **roles** (carried in the JWT via `realm_access.roles`). It cannot enforce application authorization: role-on-endpoint gates and, especially, per-entity resource rules that depend on domain state Keycloak never sees. This design adds a robust, two-layer authorization model to the app.

Decisions taken during brainstorming:

| Decision | Choice |
| --- | --- |
| **Granularity** | Permission-based **plus** resource/ownership rules (the fullest model). |
| **Source of truth** | **Hybrid.** Keycloak assigns *roles* (reuse `realm_access`). The app owns the **permission catalog**, the **role→permission mapping**, and the **resource rules**. |
| **Tenancy** | Single-tenant today, but the authorization seam is **tenant-aware** (`TenantId = Tenant.Default` for now; real tenant resolution slots in later without changing rule signatures). |
| **Management** | **Fully dynamic** — both permissions and role→permission mappings are admin-editable at runtime, with an `IsSystem` safety valve (below) to prevent silent drift/lockout. |
| **Enforcement mechanism** | **Hybrid (C):** declarative `[HasPermission]` gate at the endpoint **+** an `IResourceAuthorizer` for ownership/tenant/status inside handlers. |

### Key properties

- **No re-login on permission change.** Permissions are resolved server-side from the DB per request, never baked into the Keycloak token. An admin's mapping edit takes effect on the *next request*.
- **Modules depend only on `Identity.Abstractions`** for authz — never on Identity infrastructure (mirrors the existing `module_roles` placement).
- **Local UUID is the principal** for resource checks (per the project identity convention); the Keycloak external id stays at the JWT boundary only.

---

## 2. Placement

Follows the existing split (contract in `Identity.Abstractions`, implementation in the `Identity` module — the `module_roles` precedent).

**`Shared/AllSpice.CleanModularMonolith.Identity.Abstractions/Authorization/`** (cross-module contract):
- `HasPermissionAttribute` — `[HasPermission("invoices.approve")]`.
- `PermissionRequirement` + `PermissionAuthorizationHandler`.
- `PermissionPolicyProvider` — dynamic `IAuthorizationPolicyProvider` materializing `perm:{key}` policies on demand.
- `ICurrentUserPermissions` — the resolved permission set for the current request.
- `IResourceAuthorizer<TResource>`, `IAuthorizationRule<TResource>`, `IAuthorizationContext`, `AuthorizationActions`.
- `Permissions` — registry of code-referenced permission key constants.

**`Services/AllSpice.CleanModularMonolith.Identity/`** (owner + implementation):
- Domain: `Permission`, `Role`, `RolePermission` aggregates.
- Application/Infrastructure: bespoke `IPermissionRepository` / `IRolePermissionRepository` (+ impls — bespoke per-aggregate repositories, per project convention), `PermissionResolver` (+ caching), the startup drift reconciler.
- Api: admin endpoints under `Api/Endpoints/Authorization/`.
- EF config + migration.

---

## 3. Data model

```
Permission      ( Id, Key UNIQUE, Description, IsSystem, <audit> )
Role            ( Id, Key UNIQUE  -- mirrors Keycloak realm-role name, Description, <audit> )
RolePermission  ( RoleId FK, PermissionId FK )      -- the admin-editable mapping
AuthzMapVersion ( Id = 1, Version )                 -- bumped on any mutation; drives cache invalidation
```

- **User→Role assignment is NOT stored here** — it lives in Keycloak. `Role.Key` mirrors the realm-role *name*, which is the join key from the token into the app model.
- **`IsSystem`** is the safety valve for the "fully dynamic" choice: permission keys referenced by code (`[HasPermission(...)]`) are seeded with `IsSystem = true` and are **protected from admin deletion**. Admins still freely create/map any number of non-system permissions at runtime — dynamism preserved, silent lockout prevented.

---

## 4. Resolution & caching

A scoped `ICurrentUserPermissions` is resolved once per request:

```
realm_access role names ──► RolePermissionMap (cached) ──► HashSet<string> permission keys
                                    │
                       keyed by sorted role-name-set + AuthzMapVersion
```

- `PermissionResolver` reads the principal's role names (already present on the token after the existing realm-role claim mapping), looks up the cached role→permission map, and projects the user's permission set.
- **Cache:** `IMemoryCache` holds the role→permission map with a short TTL **and** a version stamp. Each entry records the `AuthzMapVersion` it was built at; any admin mutation bumps the version (one cheap row read), discarding stale entries. The version lives in the DB, so it is **multi-replica safe**; the bump may also publish an integration event (reusing the outbox) to proactively evict caches across replicas.
- **Result:** permission edits are effective on the next request, no re-login.

---

## 5. Enforcement — Layer 1 (coarse gate at the endpoint)

- `PermissionPolicyProvider` materializes a policy named `perm:{key}` on demand — no per-permission policy pre-registration.
- `PermissionAuthorizationHandler` succeeds iff `ICurrentUserPermissions` contains the key.
- Usage: `[HasPermission("invoices.approve")]` on a FastEndpoint → fast **403** before the handler runs; visible in OpenAPI.
- Unauthenticated requests still → **401** via the existing gateway fallback policy. Layer 1 only adds the authenticated-but-unauthorized **403**.

---

## 6. Enforcement — Layer 2 (resource/ownership inside the handler)

- After the aggregate is loaded, the handler calls `await _authorizer.AuthorizeAsync(invoice, AuthorizationActions.Approve, ct)`.
- The authorizer evaluates registered `IAuthorizationRule<TResource>` rules against a tenant-aware `IAuthorizationContext { UserId, TenantId, Permissions }`:
  - `UserId` = canonical **local UUID** from `ICurrentUserContext`.
  - `TenantId` = `Tenant.Default` for now (seam; real resolution slots in later without changing rule signatures).
- Rules express ownership / status / (future) tenant, e.g. `invoice.OwnerId == ctx.UserId && invoice.Status == Submitted`.
- On failure the authorizer yields `Result.Forbidden()` → **403** via the existing `ArdalisResultHttpExtensions` mapping (no exceptions for control flow).

**Why both layers:** Layer 1 answers "may this *kind* of user call this at all?" cheaply at the edge; Layer 2 answers "may *this* user act on *this specific* entity?" — which needs the loaded aggregate and domain state.

---

## 7. Admin API (runtime editability)

Under `Identity/Api/Endpoints/Authorization/`:

| Endpoint | Purpose | Gated by |
| --- | --- | --- |
| `GET /authz/permissions` | list catalog | `authz.read` |
| `POST /authz/permissions` · `DELETE …/{id}` | create / delete **non-system** permission | `authz.manage` |
| `GET /authz/roles` · `GET …/{key}/permissions` | view roles + mappings | `authz.read` |
| `PUT /authz/roles/{key}/permissions` | set a role's permission set (core mapping edit) | `authz.manage` |

- The authz endpoints are gated by `authz.*` permissions (Layer 1) — the system bootstraps itself; `authz.manage` / `authz.read` are seeded `IsSystem` permissions granted to an admin role.
- Every mutation is **audit-stamped** (existing `AuditableEntity` interceptor) and **bumps `AuthzMapVersion`** in the same transaction (existing `TransactionBehavior`) — which is what invalidates the resolver cache.
- `DELETE` on an `IsSystem` permission → `Result.Forbidden`.

---

## 8. Drift reconciler (honesty net for "fully dynamic")

An `IHostedService` / startup step in the Identity module:

1. Collect every permission key referenced by code — scan `[HasPermission]` attributes across registered endpoints (startup reflection) into a source-of-truth set (cross-checked against the `Permissions` registry).
2. **Seed missing** code keys as `IsSystem = true` — a freshly deployed enforced permission always exists (no lockout).
3. **Log a warning** for each DB permission no code path references (candidate dead permission) — surfaced, never auto-deleted (admins legitimately create custom ones).

Backed by an **architecture-fitness test**: every `[HasPermission("k")]` literal must resolve to a known constant in the `Permissions` registry — fails the build on a typo'd or invented key.

---

## 9. Testing strategy

Mirrors the existing test-project layout.

- **Unit:** `PermissionResolver` (role-set → permission projection; version-based cache invalidation), `PermissionAuthorizationHandler` (has/lacks key), `PermissionPolicyProvider` (materializes `perm:{key}`), each `IAuthorizationRule` (ownership/status/tenant-default).
- **Integration:** admin API round-trip (edit mapping → version bumps → next request sees new permission, no re-login); `[HasPermission]` returns 401 unauth / 403 authed-without-perm / 200 with permission; resource authorizer returns 403 on foreign owner.
- **Arch-fitness:** the `[HasPermission]`-key-exists test; plus "no module depends on Identity infrastructure for authz" (only `Identity.Abstractions`).

---

## 10. Out of scope

- **Admin UI screens.** This design ships the admin REST API + enforcement only. Admin screens are a downstream Nuxt concern (Blazor removed; Nuxt is the chosen front-end direction).
- **Real multi-tenant resolution.** Only the tenant-aware *seam* is built now (`Tenant.Default`).
- **Changes to authentication / Keycloak realm configuration** beyond reusing the existing realm-role claim.
