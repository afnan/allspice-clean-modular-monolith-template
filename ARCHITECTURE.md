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
                      (event-sourced aggregates derive from EventSourcedAggregate; their Events are the stream)
  Application/     -- Features (Commands/Queries with Handlers + Validators), Contracts, DTOs
  Infrastructure/  -- Persistence (EF DbContext, Configurations), Services, Messaging, Jobs, Extensions,
                      Projections (Marten store config lives here)
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
| Event store (opt-in) | **Marten** — one store per module in the module DB (schema `<module>`), enlisted in the module EF transaction via `MartenTransactionParticipant`; inline projections; `FetchForWriting` concurrency (ADR-0009) |

## Shared libraries

- **SharedKernel** — base entities (`Entity`, `AuditableEntity`, `SoftDeletableEntity`) + the `IAggregateRoot` marker,
  domain events, `EfRepository<TContext, TAggregate>`, value objects, Mediator pipeline behaviors, `IIntegrationEventPublisher`,
  `IUserExternalIdResolver`, `ICurrentUserProvider`, `IModuleDbContext`, EF interceptors, `DbContextHealthCheck`,
  `IFileStorageService`, `SoftDeleteQueryFilterConvention`, `MigrationRunner`.
- **Notifications.Contracts** — integration-event DTOs consumed by other modules.
- **RealTime** — `AppHub` SignalR hub + `IRealtimePublisher`.
- **Identity.Abstractions** — portal-aware JWT (`AddIdentityPortals`), claims utilities, permission-based authorization primitives (`[HasPermission]`, policy provider, `IResourceAuthorizer`).
- **ApiContracts** — response DTOs returned by endpoints (shared request/response shapes).
- **Web** — `Ardalis.Result` HTTP mapping (incl. `ExecuteFailureAsync`), `ClaimsPrincipalExtensions`.
- **Pdf** — `PdfGeneratorBase`, `PdfTheme`, `PdfFooterBuilder`.
- **EventSourcing** — `AddModuleEventStore<TStore, TContext>`, `MartenTransactionParticipant`, `MartenEventSourcedRepository`, `IEventMetadataProvider`. The only project outside module Infrastructure layers allowed to reference Marten.

## CQRS flow

`FastEndpoint` → `IMediator.Send(Command/Query)` → `Handler` (pipeline behaviors) → bespoke `Repository`
(Ardalis.Specification) → `DbContext`.

**Pipeline order (outer → inner):** Logging → DomainException → Performance → Validation → Transaction.

Commands implement `ITransactional` for automatic transaction wrapping: `TransactionBehavior` opens a DB
transaction on the module's DbContext, runs the handler, dispatches domain events (drain loop for
multi-generation events), then commits; on failure it rolls back.

**Hard constraint:** each command touches exactly ONE module's DbContext. Cross-module communication uses
Wolverine integration events via `IIntegrationEventPublisher` — never a direct write to another module's
DbContext (`TransactionBehavior` fails fast if a command dirties more than one). Domain events are in-process
and same-module only.

## Event sourcing (opt-in per aggregate)

Most aggregates are EF Core rows. An aggregate is event-sourced only when its history is the business record
(AGENTS.md golden rule 8). The reference implementation is the `Ledger` module.

**Write path.** `Account : EventSourcedAggregate` → command method guards → `Raise(event)` → `When` routes to
`Apply(TEvent)` (pure state), records the event as uncommitted **and** registers it as a domain event.
`IAccountRepository : IEventSourcedRepository<Account>` (bespoke, golden rule 4) is Marten-backed:
`LoadAsync` = `FetchForWriting` (tracks the expected version), `AddAsync` = `StartStream`, `SaveAsync` =
`AppendMany` (+ `ArchiveStream` when the aggregate called `MarkForArchive`).

**Transaction.** The module transaction is opened *lazily*: by `TransactionBehavior` at commit, or **earlier**
by `MartenTransactionParticipant` on the first repository write-intent. The Marten session is opened with
`SessionOptions.ForTransaction(npgsqlTx, shouldAutoCommit: false)` — it never commits. `TransactionBehavior`
(1) counts a participant's owner as the dirty module, (2) reuses an early transaction, (3) flushes EF then the
participant on each drain-loop iteration and dispatches domain events from both, (4) discards participants and
rolls back on failure. So: events + inline projection + EF rows + outbox envelope = one commit.

**Read path.** Inline single-stream projections (`AccountSummary`) are updated in the same transaction and
queried through `Session.OpenQuerySession()` (module repositories, not the store directly); lists never
replay streams. `HistoryAsync` returns the raw stream with
metadata (version, sequence, timestamp, correlation id, `idempotency-key` header) — the audit trail.
Archiving (`MarkForArchive` → `ArchiveStream`) removes a stream from Marten's default queries and
projections, so `HistoryAsync` queries raw events with `MaybeArchived()` and deliberately **includes archived
streams** — the audit trail must survive closing. `LoadAsync` (`FetchForWriting`) still replays an archived
stream, so a later command loads the aggregate in its terminal state and is rejected by the aggregate's own
rule (e.g. `Account.EnsureOpen`), not by a 404.

**Concurrency.** A concurrent append fails at flush with Marten's `ConcurrencyException`, translated to
`ConcurrencyConflictException` → `409 concurrency_conflict`. EF's `DbUpdateConcurrencyException` maps to the
same exception. A stream-id collision on `AddAsync` maps to `ConflictException` → 409.

**Schema & rebuild.** `AddModuleEventStore` fixes: `AutoCreate.None`, `EventAppendMode.Rich`, STJ,
correlation + header metadata, Guid streams. `Ensure{Module}ModuleDatabaseAsync` applies the schema at
startup — `ApplyEventStoreSchemaAsync<TStore>` throws `InvalidOperationException` at startup if the store's
`configure` callback registered no event type or projection (Marten would otherwise silently create no
`mt_events`/`mt_streams` tables at all). Rebuild projections with the JasperFx runner:
`dotnet run --project <Gateway> -- projections rebuild` (or `-p AccountSummary`).

**Versioning.** Keep the old CLR shape as `XxxV1` with its stored name pinned (`MapEventType<FundsDepositedV1>`
is implied by the `Upcast<…>("funds_deposited", …)` registration); give the new shape a new stored name
(`MapEventType<FundsDeposited>("funds_deposited_v2")`); register the upcaster. Never edit or delete events.

**Metadata.** `IEventMetadataProvider` (gateway: `HttpEventMetadataProvider`) stamps `CorrelationId` and the
`Idempotency-Key` on every event. Events carry **no personal data**.

**No-constructor replay.** Marten's live aggregation (`FetchForWriting`/`LiveStreamAggregation`) allocates an
event-sourced aggregate without running any constructor, then replays history straight through `Apply(TEvent)`.
No state may depend on a field/property initializer or constructor logic — every property must be assigned by
an `Apply` method, and any collection must be lazily initialized. `EventSourcedAggregate` and
`HasDomainEventsBase` already do this for their uncommitted/domain-event lists.

## Identity module

Keycloak integration with the client-credentials flow:
- **Aggregates:** `User`, `Permission`, `Role`, `RolePermission`, `AuthzMapVersion`. Authorization is
  **permission-based** (app-owned catalog + role→permission map; see ADR-0008): Keycloak authenticates and
  issues realm roles; the gateway flattens them to `ClaimTypes.Role`; the app resolves the role→permission map
  per-request via `ICurrentUserPermissions` (scoped, lazy, cached). Layer 1 — declarative endpoint gate via
  `[HasPermission("key")]` / `Policies(PermissionPolicy.For("key"))` backed by `PermissionAuthorizationHandler`;
  Layer 2 — resource/ownership via `IResourceAuthorizer`. Contracts live in `Identity.Abstractions`; implementations
  in this module. Runtime catalog management (admin endpoints, Keycloak role sync, cache eviction) is implemented.
- **Keycloak:** `KeycloakTokenProvider` (singleton, `SemaphoreSlim`-cached) + `KeycloakTokenHandler`
  (auto Bearer injection); `KeycloakDirectoryClient` for the Admin REST API.
- **Sync:** `KeycloakUserSyncJob` (Quartz) reconciles Keycloak users against the local `Users` table; an
  orphan is a Keycloak user with no local row.
- **User provisioning is the IdP's responsibility.** This template is auth-mechanism-agnostic: users are
  provisioned in Keycloak (directly, or via SSO/SAML federation), then mirrored locally by the sync job. The
  app does not create users or manage passwords — there is intentionally no in-app "invite user" flow.
- **Endpoints:** `GET /api/identity/users/{externalId}`, `GET /api/identity/users`. **Authorization admin:**
  `GET /api/identity/authz/permissions`, `POST /api/identity/authz/permissions`,
  `DELETE /api/identity/authz/permissions/{id}`, `GET /api/identity/authz/roles`,
  `GET /api/identity/authz/roles/{key}/permissions`, `PUT /api/identity/authz/roles/{key}/permissions`.

## Notifications module

Email with provider fallback + in-app channel:
- **Dev:** always MailKit (Papercut SMTP via Aspire). **Prod:** Resend → SendGrid via `EmailSenderDispatcher`; MailKit is dev-only (never a prod fallback).
- **Templates:** embedded resources in `Infrastructure/Templates/`, merged with `_Layout.html`, seeded on startup.
- **Channels:** Email, InApp (SignalR; `Recipient.UserId` MUST be a local `Guid`, resolved to external ID for SignalR).
- **Identity:** `NotificationPreference.UserId` is a local `Guid`; the dispatcher fails closed on a non-Guid recipient.

## Ledger module (reference)

`Services/AllSpice.CleanModularMonolith.Ledger` — `Account` (open / deposit / withdraw / close), events
`AccountOpened`, `FundsDeposited` (v2) / `FundsDepositedV1` (legacy, upcast), `FundsWithdrawn`,
`AccountClosed` (archives the stream — it drops out of the summary projection's default queries, but
`GET …/{accountId}/history` still returns the full trail and a further command is refused by `EnsureOpen`). `LedgerDbContext` has no entities: it owns the transaction and hosts the
co-located outbox — its EF migration is therefore empty by design (the outbox envelope tables are
`ExcludeFromMigrations`, provisioned separately by Wolverine's `Admin.MigrateAsync`). `AccountOpened` →
`NotificationRequestedIntegrationEvent` proves event-sourced write + outbox atomicity without a new Contracts
project. Endpoints under `/api/ledger/accounts` gated by `ledger:accounts.read|write`: `POST
/api/ledger/accounts` (201 + Location), `POST …/{accountId}/deposits|withdrawals|close` (204), `GET
…/{accountId}` (summary), `GET …/{accountId}/history` (audit trail). The Ledger `.csproj` carries a permanent
project-scoped `<NoWarn>$(NoWarn);MSG0005</NoWarn>` — Mediator flags an `IDomainEvent` with no handler, and
stream events other than `AccountOpened` intentionally have none. It exists to be copied or deleted — see
GETTING_STARTED.md.

## Database strategy

Each module owns its DbContext and an Aspire database resource (`identitydb`, `notificationsdb`, `ledgerdb`). The Wolverine
outbox is a **true transactional outbox**: each module's outbox envelope tables are **co-located in its own
database** (`MapWolverineEnvelopeStorage` + an enrolled ancillary store), so an integration event commits
atomically with the state change that produced it. The dedicated `messagingdb` is the Wolverine **main store**
and holds only shared infrastructure (inbox, durable local queues, scheduled messages, dead-letter). Schema
changes use **EF Core migrations** — `MigrateAsync` runs at startup with retry and a **Postgres advisory lock**
(`MigrationRunner`) so concurrent instances can't race the same migration; Wolverine envelope schemas are
provisioned at startup via `IMessageStore.Admin.MigrateAsync`. Design-time `DbContextFactory` classes read
connection details from `EF_DESIGN_*` env vars (no hardcoded password; they fail fast if none is provided).

An event-sourced module additionally has a **Marten schema** (`<module>`) in the same database;
`Ensure{Module}ModuleDatabaseAsync` applies it with `ApplyAllConfiguredChangesToDatabaseAsync` after the EF
migration (Marten uses its own advisory lock). `AutoCreate.None` at runtime — schema changes are applied at
startup, never lazily.

> **Wolverine 6 codegen:** Wolverine 6 removed the runtime code compiler from core, so any Wolverine **host**
> (the gateway, and Wolverine-starting integration tests) must reference **`WolverineFx.RuntimeCompilation`**
> for the default `TypeLoadMode.Dynamic` — without it, messaging fails to start (`no IAssemblyGenerator`). The
> gateway already references it. The gateway now runs JasperFx commands (`Program.cs` →
> `RunJasperFxCommands`): `dotnet run` with no args starts the host as before; `dotnet run -- <command>` runs a
> maintenance command instead — e.g. `codegen write` (Wolverine, for production cold-start/AOT with
> `TypeLoadMode.Static`) or `projections rebuild` (Marten, see "Event sourcing" below).

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
  build-time assertions: domain purity, module isolation, layer/naming conventions, sealed domain events
  (including event-sourced stream events, which are domain events), Marten confined to Infrastructure, Ledger
  isolation. They run as part of `dotnet test`
  and in CI. When a rule legitimately changes, update the test in the same change.
  - Marten's compile-time source generator emits an aggregate "Evolver" type (`<global__…AccountEvolver…>`)
    into the aggregate's own Domain namespace of any project that references Marten and has `Apply` methods.
    The domain-purity rules exclude source-generated types (names starting with `<`) for exactly this reason
    — don't delete that exclusion.
- `EventSourcing.IntegrationTests` / `Ledger.Infrastructure.IntegrationTests` (Testcontainers) prove
  enlistment, concurrency, projection consistency and upcasting. Full event-sourcing test inventory:
  `EventSourcing.IntegrationTests` (participant/repository/fail-fast schema guard on Postgres),
  `Ledger.Domain.UnitTests`, `Ledger.Application.UnitTests`, `Ledger.Infrastructure.IntegrationTests`
  (projection, archive, production-config upcast), and
  `Foundation.IntegrationTests/EventSourcedOutboxAtomicityTests` (post-flush rollback of stream + row +
  envelope through the real `TransactionBehavior`).
- **CI** (`.github/workflows/ci.yml`) builds (warnings-as-errors), runs all tests with coverage, and fails on
  any known-vulnerable NuGet package. **Dependabot** keeps NuGet/Actions/Docker dependencies current.
- Key decisions are recorded as ADRs under [`docs/adr/`](./docs/adr).

## Conventions

- File-scoped namespaces; private fields `_camelCase`; constants `PascalCase`.
- Central package versioning in `Directory.Packages.props` — never pin versions in individual `.csproj` files.
- `TreatWarningsAsErrors=true`; suppress only narrow, documented transitive advisories via `NuGetAuditSuppress`.
- Tests: **xUnit** + **Moq** + **coverlet**, named `{Module}.{Layer}.UnitTests` / `IntegrationTests`.

See [`AGENTS.md`](./AGENTS.md) for the enforced rules, anti-patterns, and the cross-cutting-utilities table.
