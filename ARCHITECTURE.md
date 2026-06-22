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
| Messaging | **WolverineFx** with PostgreSQL durable outbox, centralized in the gateway with typed transient-only retry |
| Integration events | **IIntegrationEventPublisher** (SharedKernel) → `WolverineIntegrationEventPublisher` (gateway). Outbox survives crashes |
| Transactional commands | `TransactionBehavior` — commands implement `ITransactional` for automatic transaction + domain-event dispatch |
| EF interceptors | `ConcurrencyDiagnosticInterceptor`, `AuditableEntityInterceptor` (attached via the SP-aware pooled registration — see AGENTS.md §6) |
| Soft delete | `SoftDeleteQueryFilterConvention` auto-filters `ISoftDelete` entities in `OnModelCreating` |
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

## Shared libraries

- **SharedKernel** — base entities (`Entity`, `AggregateRoot`, `AuditableEntity`, `SoftDeletableEntity`),
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
- **Aggregates:** `User`, `Invitation`. Authorization is **JWT-claim-based** via `ModuleRoleAuthorizationHandler`
  (no role-assignment aggregates in the database).
- **Keycloak:** `KeycloakTokenProvider` (singleton, `SemaphoreSlim`-cached) + `KeycloakTokenHandler`
  (auto Bearer injection); `KeycloakDirectoryClient` for the Admin REST API.
- **Sync:** `KeycloakUserSyncJob` (Quartz) reconciles Keycloak users against the local `Users` table; an
  orphan is a Keycloak user with no local row.
- **Invitation compensation:** `InviteUserCommandHandler` creates the Keycloak user first, then local records;
  if local persistence fails it deletes the Keycloak user to avoid orphans.
- **Endpoints:** `GET /api/identity/users/{externalId}`, `GET /api/identity/users`, `POST /api/identity/invitations`.

## Notifications module

Email with provider fallback + in-app channel:
- **Dev:** always MailKit (Papercut SMTP via Aspire). **Prod:** Resend → SendGrid → MailKit via `EmailSenderDispatcher`.
- **Templates:** embedded resources in `Infrastructure/Templates/`, merged with `_Layout.html`, seeded on startup.
- **Channels:** Email, InApp (SignalR; `Recipient.UserId` MUST be a local `Guid`, resolved to external ID for SignalR).
- **Identity:** `NotificationPreference.UserId` is a local `Guid`; the dispatcher fails closed on a non-Guid recipient.

## Database strategy

Each module owns its DbContext and an Aspire database resource (`identitydb`, `notificationsdb`); Wolverine
uses a dedicated `messagingdb` for outbox envelopes. Schema changes use **EF Core migrations** — `MigrateAsync`
runs at startup with retry (`MigrationRunner`). Design-time `DbContextFactory` classes read connection details
from `EF_DESIGN_*` env vars (no hardcoded password; they fail fast if none is provided).

```bash
EF_DESIGN_DB_PASSWORD=<local-pg-password> dotnet ef migrations add <Name> \
  --project Services/AllSpice.CleanModularMonolith.<Module>/AllSpice.CleanModularMonolith.<Module>.csproj \
  --startup-project AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
  --context <Module>DbContext --output-dir Infrastructure/Migrations
```

## Conventions

- File-scoped namespaces; private fields `_camelCase`; constants `PascalCase`.
- Central package versioning in `Directory.Packages.props` — never pin versions in individual `.csproj` files.
- `TreatWarningsAsErrors=true`; suppress only narrow, documented transitive advisories via `NuGetAuditSuppress`.
- Tests: **xUnit** + **Moq** + **coverlet**, named `{Module}.{Layer}.UnitTests` / `IntegrationTests`.

See [`AGENTS.md`](./AGENTS.md) for the enforced rules, anti-patterns, and the cross-cutting-utilities table.
