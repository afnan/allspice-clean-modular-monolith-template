# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

A .NET 10 modular monolith template (`dotnet new allspice-modular`) using Clean Architecture, CQRS, and event-driven patterns. The solution ships as a `dotnet new` template where `AllSpice.CleanModularMonolith` is the `sourceName` replaced by the user's chosen project name.

## Build & Run

```bash
# Restore and build
dotnet restore AllSpice.CleanModularMonolith.slnx
dotnet build AllSpice.CleanModularMonolith.slnx

# Run with Aspire (spins up PostgreSQL, Redis, Keycloak, Papercut SMTP)
dotnet run --project AllSpice.CleanModularMonolith.AppHost/AllSpice.CleanModularMonolith.AppHost.csproj

# Run all tests
dotnet test AllSpice.CleanModularMonolith.slnx

# Run a single test project
dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests

# Run a specific test by filter
dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests --filter "FullyQualifiedName~TestMethodName"
```

## Architecture

### Hosting Model

Everything runs as a single deployable unit. The **ApiGateway** is the sole runnable host — modules register their services into it, not as separate processes. **AppHost** is the Aspire orchestrator that provisions infrastructure (Postgres, Redis, Keycloak containers) and launches the gateway.

### Module Structure (Clean Architecture per module)

Each module under `Services/` follows this layout:
```
Services/{Module}/
  Domain/          -- Aggregates, ValueObjects, Enums, Events, Specifications
  Application/     -- Features (Commands/Queries with Handlers + Validators), Contracts, DTOs
  Infrastructure/  -- Persistence (EF DbContext, Configurations), Services, Messaging, Jobs, Extensions
  Api/             -- FastEndpoints endpoint classes
```

### How Modules Wire Into the Gateway

1. Each module exposes an `Infrastructure/Extensions/{Module}ModuleExtensions.cs` with:
   - `Add{Module}ModuleServices(builder, logger)` — registers DI (DbContext, repos, Mediator, Quartz jobs, validators)
   - `Ensure{Module}ModuleDatabaseAsync(app)` — runs `MigrateAsync` + seeds
2. `ApiGateway/Extensions/GatewayModuleRegistrationExtensions.cs` calls each module's registration in `RegisterGatewayModules()`
3. `Program.cs` calls `builder.RegisterGatewayModules()` then `app.Ensure{Module}ModuleDatabaseAsync()` for each module

**To add a new module:** Create the Clean Architecture folders under `Services/`, write the module extension, and add one line in `RegisterGatewayModules()` + one `Ensure` call in `Program.cs`.

### Key Libraries & Patterns

| Concern | Library/Pattern |
|---|---|
| CQRS / Mediator | **Mediator** (source-generated, scoped lifetime via `MediatorConfiguration.cs`) |
| Validation | **FluentValidation** + SharedKernel `ValidationBehavior` pipeline |
| API endpoints | **FastEndpoints** (not controllers) |
| Messaging | **WolverineFx** with PostgreSQL durable outbox (`WolverineFx.EntityFrameworkCore` + `WolverineFx.Postgresql`), centralized in gateway with scoped retry policies (transient exceptions only) |
| Integration events | **IIntegrationEventPublisher** abstraction in SharedKernel, implemented by `WolverineIntegrationEventPublisher` in gateway. Outbox ensures delivery survives crashes |
| Transactional commands | `TransactionBehavior` in SharedKernel pipeline — commands implement `ITransactional` marker for automatic DB transaction wrapping with domain event dispatch |
| Options validation | `ValidateDataAnnotations().ValidateOnStart()` on critical options (`KeycloakOptions`, `IdentitySyncOptions`, `NotificationDispatcherOptions`); email provider options intentionally skip for graceful fallback |
| Soft delete | `SoftDeleteQueryFilterConvention.ApplySoftDeleteFilters()` in `DbContext.OnModelCreating` — auto-filters `ISoftDelete` entities |
| Scheduling | **Quartz.NET** (registered globally in ServiceDefaults, jobs per module) |
| Realtime | **SignalR** via `Shared/RealTime/AppHub` mapped at `/hubs/app` |
| ORM | **EF Core** with PostgreSQL (Npgsql), each module owns its own DbContext |
| Specifications | **Ardalis.Specification** for query objects |
| Domain modeling | **Ardalis.GuardClauses**, **Ardalis.Result**, **Ardalis.SmartEnum** |
| Reverse proxy | **YARP** configured in `appsettings.json` under `ReverseProxy` section |
| Auth | **Keycloak** OIDC with portal-aware JWT, client credentials flow via `KeycloakTokenProvider` |
| Email | **Resend** (primary) -> **SendGrid** (fallback) -> **MailKit** (dev/last resort) via `EmailSenderDispatcher` |
| PDF generation | **PuppeteerSharp** via `Shared/AllSpice.CleanModularMonolith.Pdf` — headless Chromium, A4 output |
| Logging | **Serilog** + **OpenTelemetry** |
| Cross-module identity | **IUserExternalIdResolver** in SharedKernel — resolves local user GUIDs to Keycloak external IDs |

### Shared Libraries

- **SharedKernel** — Base entity types (`Entity`, `AggregateRoot`, `AuditableEntity`, `SoftDeletableEntity`), domain events, `EfRepository<T>`, value objects, Mediator pipeline behaviors (Logging, Performance, Validation, Transaction, DomainException), `IIntegrationEventPublisher`, `IUserExternalIdResolver`, `IModuleDbContext`, `SoftDeleteQueryFilterConvention`
- **Notifications.Contracts** — Integration event DTOs consumed by other modules to request notifications via Wolverine
- **RealTime** — `AppHub` SignalR hub and `IRealtimePublisher` abstraction for broadcasting to user groups
- **Identity.Abstractions** — Portal-aware JWT registration (`AddIdentityPortals`), claims utilities, module-role authorization
- **Web** — `Ardalis.Result` HTTP mapping extensions, `ClaimsPrincipalExtensions`
- **Pdf** — `PdfGeneratorBase` (PuppeteerSharp), `PdfTheme` (A4 CSS), `PdfFooterBuilder` (header/footer/page-frame)

### CQRS Flow

`FastEndpoint` -> `IMediator.Send(Command/Query)` -> `Handler` (with pipeline behaviors) -> `Repository` (Ardalis.Specification) -> `DbContext`

**Pipeline order (outermost to innermost):** Logging → Performance → Validation → Transaction → DomainException

Commands implement `ITransactional` for automatic transaction wrapping. The `TransactionBehavior` begins a DB transaction on the first module's DbContext, calls the handler, dispatches domain events (drain loop for multi-generation events), then commits. On failure, the transaction rolls back.

**Important constraint:** Each command should only touch ONE module's DbContext. Cross-module communication must use Wolverine integration events via `IIntegrationEventPublisher`, not direct writes to another module's DbContext. The `TransactionBehavior` logs a warning if multiple DbContexts have pending changes within a single command.

Domain events are dispatched via `MediatorDomainEventDispatcher` (registered in gateway). Cross-module communication uses Wolverine integration events via `IIntegrationEventPublisher` (not domain events). The Wolverine durable outbox ensures integration events survive process crashes.

### Identity Module

Full Keycloak integration with client credentials flow:
- **Domain:** User, Invitation, ModuleDefinition, ModuleRoleAssignment, ModuleRoleTemplate aggregates
- **Keycloak:** `KeycloakTokenProvider` (singleton, SemaphoreSlim-cached client credentials flow) + `KeycloakTokenHandler` (DelegatingHandler for auto Bearer injection)
- **KeycloakDirectoryClient:** Full Admin REST API — create/sync users, manage realm roles, temp passwords
- **Sync:** `KeycloakUserSyncJob` (Quartz) syncs Keycloak users to local User table
- **Invitation compensation:** `InviteUserCommandHandler` creates the Keycloak user first, then local records. If local persistence fails, the handler compensates by deleting the Keycloak user to prevent orphans.
- **API endpoints:** `GET /api/identity/users/{externalId}`, `GET /api/identity/users`, `POST /api/identity/invitations`

### Notifications Module

Email delivery with provider fallback chain:
- **Development:** Always MailKit (Papercut SMTP container via Aspire)
- **Production:** Resend -> SendGrid -> MailKit fallback via `EmailSenderDispatcher`
- **HTML Templates:** Embedded resources in `Infrastructure/Templates/`, loaded by `EmailTemplateLoader`, merged with `_Layout.html`, seeded to DB on startup
- **Channels:** Email, InApp (SignalR with external ID resolution via `IUserExternalIdResolver`)
- **Templates:** `invitation-created`, `registration-welcome`, `role-assigned`, `role-revoked`, `password-reset`, `profile-updated`

### FastEndpoints Assembly Discovery

FastEndpoints uses explicit assembly discovery (auto-discovery disabled) in `GatewayServiceCollectionExtensions`. When adding a new module, add its assembly to the `Assemblies` array.

### Database Strategy

Each module has its own DbContext and Aspire database resource (e.g., `notificationsdb`, `identitydb`). Schema changes use **EF Core migrations** — `MigrateAsync` runs at startup with retry logic. Design-time factories (`IDesignTimeDbContextFactory`) exist in each module for CLI migration generation without running infrastructure. Wolverine uses a dedicated `messagingdb` for durable outbox envelope storage.

To generate a new migration:
```bash
dotnet ef migrations add <MigrationName> \
  --project Services/AllSpice.CleanModularMonolith.<Module>/AllSpice.CleanModularMonolith.<Module>.csproj \
  --startup-project AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
  --context <Module>DbContext \
  --output-dir Infrastructure/Migrations
```

## Conventions

- **File-scoped namespaces** enforced (`csharp_style_namespace_declarations = file_scoped:warning`)
- **Private fields** prefixed with `_` (camelCase): `_myField`
- **Constants** use PascalCase
- Central package versioning via `Directory.Packages.props` — never specify versions in individual `.csproj` files
- Test projects use **xunit** with **Moq** and **coverlet**, naming: `{Module}.{Layer}.UnitTests` or `{Module}.{Layer}.IntegrationTests`
- Template variables `{{ProjectName}}` and `{{ProjectNameLower}}` appear in config files — these are replaced when users scaffold from the template

## Use Serena MCP for Semantic Code Analysis

Serena MCP is available for advanced code retrieval and editing capabilities.

**When to use Serena:**
- Symbol-based code navigation (find definitions, references, implementations)
- Precise code manipulation in structured codebases
- Prefer symbol-based operations over file-based grep/sed when available

**Key tools:**
- `find_symbol` - Find symbol by name across the codebase
- `find_referencing_symbols` - Find all symbols that reference a given symbol
- `get_symbols_overview` - Get overview of top-level symbols in a file
- `read_file` - Read file content within the project directory
