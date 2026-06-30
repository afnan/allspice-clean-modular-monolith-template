# Architecture & Patterns

How a project generated from this template is built. This is the **descriptive** reference; the
**prescriptive** rules (DO/DON'T, golden rules, recipes, Definition of Done) live in
[`AGENTS.md`](./AGENTS.md). Both files ship with a scaffolded project; read `AGENTS.md` first if you're an agent.

> Note for template maintainers: guidance for developing the *template itself* lives in `CLAUDE.md`,
> which is intentionally excluded from generated projects.

## Build & run

```bash
dotnet restore AllSpice.CleanModularMonolith.slnx
dotnet build   AllSpice.CleanModularMonolith.slnx          # 0 warnings (TreatWarningsAsErrors=true)
dotnet test    AllSpice.CleanModularMonolith.slnx

# Run the whole stack via Aspire (PostgreSQL, Redis, Keycloak, Papercut SMTP, Azurite)
dotnet run --project AllSpice.CleanModularMonolith.AppHost/AllSpice.CleanModularMonolith.AppHost.csproj

# A single test project / a single test
dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests
dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests --filter "FullyQualifiedName~TestMethodName"
```

## Hosting model

Everything runs as a single deployable unit. The **ApiGateway** is the sole runnable host — modules
register their services into it, not as separate processes. **AppHost** is the Aspire orchestrator that
provisions infrastructure (Postgres, Redis, Keycloak, Azurite, Papercut SMTP) and launches the gateway.

## Module structure (Clean Architecture per module)

```
Services/{Module}/
  Domain/          -- Aggregates, ValueObjects, Enums, Events, Specifications
  Application/     -- Features (Commands/Queries with Handlers + Validators), Contracts, DTOs
  Infrastructure/  -- Persistence (EF DbContext, Configurations), Services, Messaging, Jobs, Extensions
  Api/             -- FastEndpoints endpoint classes
```

### How modules wire into the gateway

1. Each module exposes `Infrastructure/Extensions/{Module}ModuleExtensions.cs` with:
   - `Add{Module}ModuleServices(builder, logger)` — registers DI (DbContext, repos, Mediator, Quartz jobs, validators).
   - `Ensure{Module}ModuleDatabaseAsync(app)` — runs `MigrateAsync` + seeds.
2. `ApiGateway/Extensions/GatewayModuleRegistrationExtensions.cs` calls each module in `RegisterGatewayModules()`.
3. `Program.cs` calls `builder.RegisterGatewayModules()` then `app.Ensure{Module}ModuleDatabaseAsync()` per module.

**To add a module:** create the layer folders under `Services/`, write the module extension, add one line in
`RegisterGatewayModules()` + one `Ensure` call in `Program.cs`, register the API assembly in
`GatewayServiceCollectionExtensions`, and add the Aspire DB resource + a design-time `DbContextFactory`.

## Key libraries & patterns

| Concern | Library/Pattern |
|---|---|
| CQRS / Mediator | **Mediator** (source-generated, scoped lifetime via `MediatorConfiguration.cs`) |
| Validation | **FluentValidation** + SharedKernel `ValidationBehavior` pipeline |
| API endpoints | **FastEndpoints** (not controllers) |
| Messaging | **WolverineFx** with a per-module **co-located transactional outbox** (envelopes in each module DB; `messagingdb` = infra-only main store), typed transient-only retry |
| Integration events | **IIntegrationEventPublisher** (SharedKernel) → `WolverineIntegrationEventPublisher` (gateway). Envelope commits atomically with the state change; survives crashes |
| Transactional commands | `TransactionBehavior` — repositories stage only; the behavior owns the single flush+commit on the dirty module context (a real unit of work) + domain-event dispatch |
| EF interceptors | `ConcurrencyDiagnosticInterceptor`, `AuditableEntityInterceptor` (attached via the SP-aware `AddDbContextWithWolverineIntegration` registration — see AGENTS.md §6) |
| Soft delete | `SoftDeleteInterceptor` turns `Remove()` into a soft delete; `SoftDeleteQueryFilterConvention` hides `ISoftDelete` rows |
| Scheduling | **Quartz.NET** (registered in ServiceDefaults, jobs per module) |
| Realtime | **SignalR** via `Shared/RealTime/AppHub` at `/hubs/app` |
| ORM | **EF Core** + PostgreSQL (Npgsql); each module owns its DbContext |
| Specifications | **Ardalis.Specification** for query objects |
| Domain modeling | **Ardalis.GuardClauses**, **Ardalis.Result**, **Ardalis.SmartEnum** |
| Result → HTTP | `Ardalis.Result` mapping + `ExecuteFailureAsync` (status → RFC7807) in `Web` |
| File storage | `IFileStorageService` + Azure Blob impl (`BlobConnection`, Azurite in dev) |
| Reverse proxy | **YARP** (`appsettings.json` → `ReverseProxy`) |
| Auth | **Keycloak** OIDC, portal-aware JWT, client-credentials via `KeycloakTokenProvider` |
| Email | **Resend** → **SendGrid** → **MailKit** via `EmailSenderDispatcher` |
| PDF | **PuppeteerSharp** via `Shared/...Pdf` (headless Chromium, A4) |
| Observability | **Serilog** + **OpenTelemetry**; `DbContextHealthCheck<TContext>` for DB probes |
| Cross-module identity | **IUserExternalIdResolver** — local `Guid` ↔ Keycloak external ID |
| Clock | **`TimeProvider`** everywhere (`GetUtcNow()`); domain methods take an explicit `nowUtc` — no direct `UtcNow` |
| HTTP idempotency | `IdempotencyMiddleware` — opt-in `Idempotency-Key` header on POST/PUT/PATCH; Redis-backed replay |
| Error contract | RFC7807 problem+json with a machine-readable `code` (auto-derived from `DomainException` type) |
| PII in logs | `[SensitiveData]` on request properties → redacted by `LoggingBehavior`; responses never logged |
| Architecture enforcement | **NetArchTest** fitness tests (`tests/...Architecture.Tests`) assert the golden rules at build time |

## Shared libraries

- **SharedKernel** — base entities (`Entity`, `AuditableEntity`, `SoftDeletableEntity`) + the `IAggregateRoot` marker,
  domain events, `EfRepository<T>`, value objects, Mediator pipeline behaviors, `IIntegrationEventPublisher`,
  `IUserExternalIdResolver`, `ICurrentUserProvider`, `IModuleDbContext`, EF interceptors, `DbContextHealthCheck`,
  `IFileStorageService`, `SoftDeleteQueryFilterConvention`, `MigrationRunner`.
- **Notifications.Contracts** — integration-event DTOs consumed by other modules.
- **RealTime** — `AppHub` SignalR hub + `IRealtimePublisher`.
- **Identity.Abstractions** — portal-aware JWT (`AddIdentityPortals`), claims utilities, module-role authorization.
- **Web** — `Ardalis.Result` HTTP mapping (incl. `ExecuteFailureAsync`), `ClaimsPrincipalExtensions`.
- **Pdf** — `PdfGeneratorBase`, `PdfTheme`, `PdfFooterBuilder`.

## CQRS flow

`FastEndpoint` → `IMediator.Send(Command/Query)` → `Handler` (pipeline behaviors) → bespoke `Repository`
(Ardalis.Specification) → `DbContext`.

**Pipeline order (outer → inner):** Logging → Performance → Validation → Transaction → DomainException.

Commands implement `ITransactional` for automatic transaction wrapping: `TransactionBehavior` opens a DB
transaction on the module's DbContext, runs the handler, dispatches domain events (drain loop for
multi-generation events), then commits; on failure it rolls back.

**Hard constraint:** each command touches exactly ONE module's DbContext. Cross-module communication uses
Wolverine integration events via `IIntegrationEventPublisher` — never a direct write to another module's
DbContext (`TransactionBehavior` fails fast if a command dirties more than one). Domain events are in-process
and same-module only.

## Identity module

Keycloak integration with the client-credentials flow:
- **Aggregates:** `User`, `Permission`, `Role`, `RolePermission`, `AuthzMapVersion`. Authorization is
  **permission-based** (app-owned catalog + role→permission map; see ADR-0008): Keycloak authenticates and
  issues realm roles; the gateway flattens them to `ClaimTypes.Role`; the app resolves the role→permission map
  per-request via `ICurrentUserPermissions` (scoped, lazy, cached). Layer 1 — declarative endpoint gate via
  `[HasPermission("key")]` / `Policies(PermissionPolicy.For("key"))` backed by `PermissionAuthorizationHandler`;
  Layer 2 — resource/ownership via `IResourceAuthorizer`. Contracts live in `Identity.Abstractions`; implementations
  in this module. Runtime catalog management (admin endpoints, Keycloak role sync, cache eviction) is forthcoming.
- **Keycloak:** `KeycloakTokenProvider` (singleton, `SemaphoreSlim`-cached) + `KeycloakTokenHandler`
  (auto Bearer injection); `KeycloakDirectoryClient` for the Admin REST API.
- **Sync:** `KeycloakUserSyncJob` (Quartz) reconciles Keycloak users against the local `Users` table; an
  orphan is a Keycloak user with no local row.
- **User provisioning is the IdP's responsibility.** This template is auth-mechanism-agnostic: users are
  provisioned in Keycloak (directly, or via SSO/SAML federation), then mirrored locally by the sync job. The
  app does not create users or manage passwords — there is intentionally no in-app "invite user" flow.
- **Endpoints:** `GET /api/identity/users/{externalId}`, `GET /api/identity/users`.

## Notifications module

Email with provider fallback + in-app channel:
- **Dev:** always MailKit (Papercut SMTP via Aspire). **Prod:** Resend → SendGrid → MailKit via `EmailSenderDispatcher`.
- **Templates:** embedded resources in `Infrastructure/Templates/`, merged with `_Layout.html`, seeded on startup.
- **Channels:** Email, InApp (SignalR; `Recipient.UserId` MUST be a local `Guid`, resolved to external ID for SignalR).
- **Identity:** `NotificationPreference.UserId` is a local `Guid`; the dispatcher fails closed on a non-Guid recipient.

## Database strategy

Each module owns its DbContext and an Aspire database resource (`identitydb`, `notificationsdb`). The Wolverine
outbox is a **true transactional outbox**: each module's outbox envelope tables are **co-located in its own
database** (`MapWolverineEnvelopeStorage` + an enrolled ancillary store), so an integration event commits
atomically with the state change that produced it. The dedicated `messagingdb` is the Wolverine **main store**
and holds only shared infrastructure (inbox, durable local queues, scheduled messages, dead-letter). Schema
changes use **EF Core migrations** — `MigrateAsync` runs at startup with retry and a **Postgres advisory lock**
(`MigrationRunner`) so concurrent instances can't race the same migration; Wolverine envelope schemas are
provisioned at startup via `IMessageStore.Admin.MigrateAsync`. Design-time `DbContextFactory` classes read
connection details from `EF_DESIGN_*` env vars (no hardcoded password; they fail fast if none is provided).

> **Wolverine 6 codegen:** Wolverine 6 removed the runtime code compiler from core, so any Wolverine **host**
> (the gateway, and Wolverine-starting integration tests) must reference **`WolverineFx.RuntimeCompilation`**
> for the default `TypeLoadMode.Dynamic` — without it, messaging fails to start (`no IAssemblyGenerator`). The
> gateway already references it. For production cold-start/AOT you can instead pre-generate code
> (`dotnet run -- codegen write` + `TypeLoadMode.Static`).

```bash
EF_DESIGN_DB_PASSWORD=<local-pg-password> dotnet ef migrations add <Name> \
  --project Services/AllSpice.CleanModularMonolith.<Module>/AllSpice.CleanModularMonolith.<Module>.csproj \
  --startup-project AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
  --context <Module>DbContext --output-dir Infrastructure/Migrations
```

## Deployment

The whole system ships as **one container** — the `ApiGateway` host. `AppHost` (Aspire) is **development
only**. A multi-stage [`Dockerfile`](./Dockerfile) builds the gateway; [`deploy/`](./deploy) has a sample
Kubernetes Deployment/Service and a deploy guide. Liveness (`/alive`) and readiness (`/health`) endpoints are
mapped in **every** environment (orchestrators need them in production); keep them off the public ingress. See
[`deploy/README.md`](./deploy/README.md).

## Testing & enforcement

- **xUnit + Moq + coverlet**; integration tests use **Testcontainers** (Postgres) and SQLite.
- **Architecture-fitness tests** (`tests/...Architecture.Tests`, NetArchTest) turn the golden rules into
  build-time assertions: domain purity, module isolation, layer/naming conventions, sealed domain events. They
  run as part of `dotnet test` and in CI. When a rule legitimately changes, update the test in the same change.
- **CI** (`.github/workflows/ci.yml`) builds (warnings-as-errors), runs all tests with coverage, and fails on
  any known-vulnerable NuGet package. **Dependabot** keeps NuGet/Actions/Docker dependencies current.
- Key decisions are recorded as ADRs under [`docs/adr/`](./docs/adr).

## Conventions

- File-scoped namespaces; private fields `_camelCase`; constants `PascalCase`.
- Central package versioning in `Directory.Packages.props` — never pin versions in individual `.csproj` files.
- `TreatWarningsAsErrors=true`; suppress only narrow, documented transitive advisories via `NuGetAuditSuppress`.
- Tests: **xUnit** + **Moq** + **coverlet**, named `{Module}.{Layer}.UnitTests` / `IntegrationTests`.

See [`AGENTS.md`](./AGENTS.md) for the enforced rules, anti-patterns, and the cross-cutting-utilities table.
