# 0008 — In-app permission-based authorization (RBAC)

- Status: Accepted
- Date: 2026-06-30

## Context

Keycloak authenticates users and assigns realm roles (carried in the JWT). It cannot enforce application
authorization: coarse role/permission gates on endpoints, module-level access, and per-entity resource rules
that depend on domain state the IdP never sees.

The repo also carried a `module_roles` mechanism (`ModuleRole*`, `HasModuleRole`, `AddModuleRoleAuthorization`)
that was wired but **dead** — no policy registered, no endpoint consuming it, and the `module_roles` claim never
emitted. Shipping it alongside a real authorization model would teach two overlapping ways to authorize.

## Decision

Authorization is owned by the app as a **permission-based model**; Keycloak is the source of role *assignment*
only (hybrid). The app never stores user→role.

- **Source of truth.** Keycloak issues realm roles (reused from `realm_access` → `ClaimTypes.Role`). The app owns
  the permission catalog, the role→permission mapping, and resource rules. `Role` rows are synced from Keycloak
  (`RoleSyncJob` + a new `KeycloakRoleClient.GetAllRealmRolesAsync`); the job degrades gracefully when Keycloak is
  unconfigured (app still boots, `/health` Degraded).
- **Two enforcement layers.** A declarative `[HasPermission("key")]` gate at the endpoint (dynamic
  `IAuthorizationPolicyProvider` materializing `perm:{key}` policies, delegating to the default provider for the
  existing `authenticated` / `allow-anonymous` / fallback policies), plus a thin `IResourceAuthorizer` facade over
  the built-in `AuthorizationHandler<TRequirement, TResource>` for ownership/tenant/status. The facade keeps
  HttpContext out of mediator handlers by sourcing identity from `ICurrentUserContext` (ADR-0005).
- **Module-scoped.** Modules are permission namespaces with a two-tier gate: a coarse `module.access` permission at
  the module endpoint group plus fine `module:action` permissions. Each module self-declares its keys via an
  `IModulePermissionManifest`.
- **Fully dynamic, with a drift valve.** Catalog and mappings are admin-editable at runtime. Code-referenced keys
  are seeded `IsSystem` (deletion-protected) so an admin can never silently lock everyone out. A startup reconciler
  (idempotent, guarded by `pg_advisory_lock` like `MigrationRunner`) seeds manifest + `[HasPermission]` keys; an
  architecture-fitness test (ADR-0007) pins every literal key to the `Permissions` registry.
- **First-admin bootstrap** is config-driven (`Authorization:BootstrapAdminRole` → `authz.manage` / `authz.read`,
  re-applied idempotently on startup), avoiding a hardcoded role name in the template.
- **Resolution + propagation.** Roles→permissions are resolved server-side per request (so changes need no
  re-login), cached in-memory, and scoped to local endpoints (not proxied routes). A mapping mutation bumps a
  durable `AuthzMapVersion` in the same transaction and publishes a best-effort Redis pub-sub nudge so every
  replica evicts its map. A 60s backstop TTL and in-process fallback (when Redis is absent) keep it correct.
- The unused `module_roles` mechanism is **removed**.

## Consequences

- One authorization model in the template, not two; the `module_roles` plumbing is deleted.
- Permission changes propagate near-instantly across replicas without re-issuing tokens; bounded to ≤60s if a
  pub-sub message is lost.
- Resource rules live with their module's aggregate; handlers stay HttpContext-free.
- Keycloak stays the role-assignment authority; in-app authz depends on it only for *which roles* a user has.
- Permission resolution adds per-request work, deliberately scoped to local endpoints to avoid taxing the YARP
  proxy path (see the `CurrentUserResolutionMiddleware` P3 note in `TODOS.md`).
- Per-user permission overrides and an authz-change audit view are deferred (`TODOS.md`).
- Full design, data model, and test plan: `docs/superpowers/specs/2026-06-29-app-rbac-design.md`.
