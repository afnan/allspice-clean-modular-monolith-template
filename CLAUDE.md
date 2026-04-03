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

Everything runs as a single deployable unit. The **ApiGateway** is the sole runnable host — modules register their services into it, not as separate processes. **AppHost** is the Aspire orchestrator that provisions infrastructure (Postgres, Redis, Keycloak containers) and launches the gateway + portal apps.

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
   - `Ensure{Module}ModuleDatabaseAsync(app)` — runs EnsureCreatedAsync + seeds
2. `ApiGateway/Extensions/GatewayModuleRegistrationExtensions.cs` calls each module's registration in `RegisterGatewayModules()`
3. `Program.cs` calls `builder.RegisterGatewayModules()` then `app.Ensure{Module}ModuleDatabaseAsync()` for each module

**To add a new module:** Create the Clean Architecture folders under `Services/`, write the module extension, and add one line in `RegisterGatewayModules()` + one `Ensure` call in `Program.cs`.

### Key Libraries & Patterns

| Concern | Library/Pattern |
|---|---|
| CQRS / Mediator | **Mediator** (source-generated, scoped lifetime via `MediatorConfiguration.cs`) |
| Validation | **FluentValidation** + SharedKernel `ValidationBehavior` pipeline |
| API endpoints | **FastEndpoints** (not controllers) |
| Messaging | **WolverineFx** (in-memory by default, handlers in `Infrastructure/Messaging/`, centralized in gateway with retry policies) |
| Integration events | **IIntegrationEventPublisher** abstraction in SharedKernel, implemented by `WolverineIntegrationEventPublisher` in gateway |
| Scheduling | **Quartz.NET** (registered globally in ServiceDefaults, jobs per module) |
| Realtime | **SignalR** via `Shared/RealTime/AppHub` mapped at `/hubs/app` |
| ORM | **EF Core** with PostgreSQL (Npgsql), each module owns its own DbContext |
| Specifications | **Ardalis.Specification** for query objects |
| Domain modeling | **Ardalis.GuardClauses**, **Ardalis.Result**, **Ardalis.SmartEnum** |
| Reverse proxy | **YARP** configured in `appsettings.json` under `ReverseProxy` section |
| Auth | **Keycloak** OIDC with portal-aware JWT (`Identity.Abstractions/Authentication/`) |
| Logging | **Serilog** + **OpenTelemetry** |
| Cross-module identity | **IUserExternalIdResolver** in SharedKernel — resolves local user GUIDs to Keycloak external IDs |

### Shared Libraries

- **SharedKernel** — Base entity types (`Entity`, `AggregateRoot`, `AuditableEntity`), domain events, `EfRepository<T>`, value objects, Mediator pipeline behaviors (Logging, Performance, Validation), exception types
- **Notifications.Contracts** — Integration event DTOs consumed by other modules to request notifications via Wolverine
- **RealTime** — `AppHub` SignalR hub and `IRealtimePublisher` abstraction for broadcasting to user groups
- **Identity.Abstractions** — Portal-aware JWT registration (`AddIdentityPortals`), claims utilities, module-role authorization
- **Web** — `Ardalis.Result` HTTP mapping extensions, `ClaimsPrincipalExtensions`

### CQRS Flow

`FastEndpoint` -> `IMediator.Send(Command/Query)` -> `Handler` (with FluentValidation + pipeline behaviors) -> `Repository` (Ardalis.Specification) -> `DbContext`

Domain events are dispatched via `MediatorDomainEventDispatcher` (registered in gateway). Cross-module communication uses Wolverine integration events via `IIntegrationEventPublisher` (not domain events).

### Database Strategy

Each module has its own DbContext and Aspire database resource (e.g., `notificationsdb`, `identitydb`). Databases are auto-created via `EnsureCreatedAsync` at startup (no EF migrations currently).

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
