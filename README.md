# AllSpice Clean Modular Monolith

A production-ready .NET 10 modular monolith template (`dotnet new allspice-modular`) using Clean Architecture, CQRS, and event-driven patterns. Ships with full Keycloak integration, multi-provider email delivery, PuppeteerSharp PDF generation, and a complete Identity + Notifications module stack.

## Why this template

Most starters give you folders. This one encodes the decisions **and enforces them**:

- **0-warning builds, enforced.** `TreatWarningsAsErrors=true` plus **architecture-fitness tests** (NetArchTest)
  that fail the build when a module reaches across a boundary or a permission key drifts from its registry. The
  golden rules are executable, not aspirational.
- **Correct-by-construction cross-module messaging.** A **co-located transactional outbox** per module (Wolverine)
  publishes an integration event only if its transaction commits — no dual-write races, no lost events.
- **Deterministic and testable.** `TimeProvider`-based clock everywhere (no hidden `DateTime.UtcNow`), so time is
  injectable and tests don't flake on the wall clock.
- **Safe by default at the edges.** HTTP idempotency (`Idempotency-Key`) for retry-safe writes, RFC7807 problem
  responses with machine-readable `code`s, and PII-safe logging (`[SensitiveData]` redaction in the pipeline).
- **Boots without its dependencies.** Keycloak, Redis, and email providers are optional at startup — the app comes
  up `Degraded` rather than dead and self-heals when they appear, so a freshly generated project runs on
  `dotnet run` before you've wired anything.
- **Decisions are written down.** Every non-obvious choice has an **ADR** under `docs/adr/`; the prescriptive agent
  rules (`AGENTS.md`) and architecture reference (`ARCHITECTURE.md`) ship with each generated project.

## Features

- **API Gateway with YARP** for routing, Redis output caching, and JWT validation
- **Identity module** with Keycloak Admin API integration — user sync/mirroring, role management, client credentials token caching (auth-agnostic: the IdP provisions users via direct admin or SSO/SAML)
- **Permission-based authorization (RBAC)** — app-owned permission catalog + role→permission map. Declarative `[HasPermission("key")]` endpoint gates (dynamic `IAuthorizationPolicyProvider`), plus an `IResourceAuthorizer` facade for ownership/tenant/status rules that keeps `HttpContext` out of handlers. Roles sync from Keycloak; mappings are admin-editable at runtime and propagate across replicas via a Redis pub-sub eviction nudge (60s TTL backstop + in-process fallback). Code-referenced keys are seeded `IsSystem` (deletion-protected) so an admin can never lock everyone out — see [ADR-0008](docs/adr/0008-in-app-permission-based-authorization.md)
- **Notifications module** with Resend/SendGrid providers (MailKit/Papercut for local dev), HTML email templates (embedded resources), SignalR in-app delivery, Quartz stale-pending monitor
- **PuppeteerSharp PDF library** — headless Chromium, A4 output, reusable theme CSS, header/footer page-frame
- **Realtime hub** sharing SignalR infrastructure across modules with automatic user groups
- **Wolverine messaging** with PostgreSQL durable outbox for reliable event-driven cross-module communication
- **Quartz.NET scheduling** with per-module jobs (Keycloak user sync, stale-pending notifications monitor)
- **Aspire AppHost** to spin up PostgreSQL, Redis, Keycloak, and Papercut SMTP in one command
- **Central package management** with .NET 10, Clean Architecture patterns powered by Ardalis libraries
- **FastEndpoints** with explicit assembly discovery (not controllers)
- **Serilog + OpenTelemetry** logging and tracing (PII-safe: `[SensitiveData]` redaction in the logging pipeline)
- **`TimeProvider`-based clock** throughout for deterministic, testable time
- **Architecture-fitness tests** (NetArchTest) that enforce the golden rules at build time
- **HTTP idempotency** (`Idempotency-Key`) for safe POST/PUT/PATCH retries, plus RFC7807 errors with machine-readable `code`s
- **Container + Kubernetes ready** — multi-stage `Dockerfile`, `deploy/` k8s sample, production liveness/readiness probes
- **Hardened CI** — coverage reporting + vulnerable-package gate, Dependabot, and ADRs under `docs/adr/`

## Getting Started

### Install the Template

```bash
dotnet new install .
```

### Create a New Project

```bash
dotnet new allspice-modular -n Contoso.Erp
cd Contoso.Erp
dotnet restore Contoso.Erp.slnx
dotnet build Contoso.Erp.slnx
```

### Run with Aspire

```bash
dotnet run --project Contoso.Erp.AppHost/Contoso.Erp.AppHost.csproj
```

Spins up PostgreSQL, Redis, Keycloak, and Papercut SMTP (dev only).

## Project Layout

```
{{ProjectName}}/
|- {{ProjectName}}.ApiGateway/           -- Sole runnable host, YARP, FastEndpoints, SignalR
|- {{ProjectName}}.AppHost/              -- Aspire orchestrator (Postgres, Redis, Keycloak, Papercut)
|- {{ProjectName}}.ServiceDefaults/      -- OpenTelemetry, resilience, service discovery, Quartz hosting
|- Services/{{ProjectName}}.Identity/    -- User aggregate, Keycloak sync, RBAC
|- Services/{{ProjectName}}.Notifications/ -- Multi-channel delivery, templates, preferences
|- Shared/{{ProjectName}}.SharedKernel/  -- Base entities, domain events, EfRepository, pipeline behaviors
|- Shared/{{ProjectName}}.Pdf/           -- PuppeteerSharp PDF generation, theme CSS, footer builder
|- Shared/{{ProjectName}}.RealTime/      -- SignalR hub, IRealtimePublisher
|- Shared/{{ProjectName}}.Notifications.Contracts/ -- Integration event DTOs
|- Shared/{{ProjectName}}.Identity.Abstractions/   -- Portal-aware JWT, claims, permission-based authorization primitives
|- Shared/{{ProjectName}}.Web/           -- Ardalis.Result HTTP extensions
\- Directory.Packages.props              -- Central NuGet version management
```

## Modules

| Module | Highlights |
| --- | --- |
| **Identity** | User aggregate, `KeycloakTokenProvider` (client credentials flow), `KeycloakDirectoryClient` (Admin REST API), `KeycloakUserSyncJob` (mirrors IdP users locally), `KeycloakRoleClient` + `RoleSyncJob` (mirrors realm roles), permission-based RBAC (catalog + role→permission map, `[HasPermission]` gates, admin CRUD endpoints), health checks |
| **Notifications** | Email (Resend/SendGrid; MailKit dev-only), InApp (SignalR), HTML templates (embedded resources + DB seeding), `NotificationContentBuilder` with `{{token}}` replacement, Quartz stale-pending monitor, Wolverine consumer |
| **ApiGateway** | FastEndpoints (explicit assembly discovery), YARP reverse proxy, SignalR hub mapping, Redis output caching, centralized Wolverine durable outbox registration |
| **AppHost** | Aspire orchestrator: PostgreSQL, Redis, Keycloak (dev + prod modes), Papercut SMTP |

## Identity & Authentication

- **Keycloak** acts as the identity provider with client credentials flow for server-to-server API calls
- `KeycloakTokenProvider` caches tokens with SemaphoreSlim thread safety, refreshing shortly before expiry (30s margin)
- `KeycloakTokenHandler` (DelegatingHandler) auto-injects Bearer tokens into HTTP clients
- `KeycloakDirectoryClient` provides full Admin REST API: create users, manage roles, reset passwords, paginated user listing
- `KeycloakUserSyncJob` (Quartz) periodically syncs Keycloak users to local User table
- **Auth-agnostic by design:** users are provisioned in the IdP (Keycloak directly, or federated via SSO/SAML) and mirrored locally by the sync job. The template intentionally ships no in-app "invite user" / password-creation flow, so it works unchanged whether you use Keycloak-local accounts or external SSO/SAML.

## Authorization

Authentication (who you are) is Keycloak's job; **authorization (what you can do) is owned by the app** as a
permission-based model (see [ADR-0008](docs/adr/0008-in-app-permission-based-authorization.md)). Keycloak issues
realm roles in the JWT; the app owns the permission catalog, the role→permission map, and per-resource rules.

- **Two enforcement layers.** A declarative endpoint gate — `[HasPermission("notifications:preferences.manage")]` /
  `Policies(PermissionPolicy.For("key"))` — materialized on demand by a dynamic `IAuthorizationPolicyProvider`
  (which delegates unknown policies to the default provider), plus a thin `IResourceAuthorizer` facade over
  ASP.NET's `AuthorizationHandler<TRequirement, TResource>` for ownership / tenant / status checks. The facade
  sources identity from `ICurrentUserContext`, so mediator handlers stay `HttpContext`-free.
- **Module-scoped permissions.** Each module self-declares its keys via an `IModulePermissionManifest`; a coarse
  `{module}.access` permission gates the module's endpoint group, with fine-grained `{module}:{action}` keys inside.
- **Dynamic, with guardrails.** The catalog and mappings are admin-editable at runtime via CRUD endpoints. A
  startup reconciler (idempotent, guarded by `pg_advisory_lock`) seeds every manifest + `[HasPermission]` key as
  `IsSystem` (deletion-protected); an architecture-fitness test pins every literal permission string to the
  `Permissions` registry so drift fails the build.
- **Resolved per request, cached, propagated.** Roles→permissions resolve server-side on each request (so grants
  take effect without re-login), cached in-memory. A mapping change bumps a durable `AuthzMapVersion` in the same
  transaction and fires a best-effort Redis pub-sub nudge so every replica evicts its map near-instantly; a 60s TTL
  backstop and an in-process fallback (when Redis is absent) keep it correct.
- **First-admin bootstrap** is config-driven (`Authorization:BootstrapAdminRole` → `authz.manage` / `authz.read`,
  re-applied idempotently on startup) — no hardcoded role name baked into the template.

## Email Delivery

Provider selection via `EmailSenderDispatcher`:
- **Development:** Always MailKit (Papercut SMTP container via Aspire)
- **Production:** Resend -> SendGrid. MailKit is dev-only and is **never** a silent production fallback;
  if no provider is configured (or every configured provider fails), the dispatcher fails fast rather than
  dropping mail to a non-existent local SMTP server.

HTML email templates stored as embedded resources, loaded by `EmailTemplateLoader`, merged with `_Layout.html` layout, and seeded/synced to database on startup. Templates: `registration-welcome`, `role-assigned`, `role-revoked`, `password-reset`, `profile-updated`.

## Configuration

Key environment variables (set via Aspire AppHost or appsettings):

| Setting | Description |
| --- | --- |
| `Identity:Keycloak:ServiceName` | Aspire service discovery name for Keycloak |
| `Identity:Keycloak:Realm` | Keycloak realm name |
| `Identity:Keycloak:ClientId` | OAuth client ID for client credentials flow |
| `Identity:Keycloak:ClientSecret` | OAuth client secret |
| `Identity:Keycloak:ApiToken` | Static admin token (fallback when ClientId not set) |
| `Notifications:Resend:ApiKey` | Resend API key |
| `Notifications:Resend:FromAddress` | Resend from address |
| `Notifications:SendGrid:ApiKey` | SendGrid API key |
| `Notifications:SendGrid:FromAddress` | SendGrid from address |
| `Notifications:Smtp:Host` | SMTP host (MailKit) |
| `Notifications:Smtp:Port` | SMTP port |

## License

MIT

---

Built with .NET 10, Clean Architecture, and modular monolith principles.
