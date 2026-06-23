# Phase 0 Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ITransactional` commands truly atomic and integration events truly atomic with their state change, by introducing a real Unit of Work and a hybrid (co-located) transactional outbox — proven with real-Postgres tests.

**Architecture:** Repositories stage writes only; `TransactionBehavior` opens one transaction on the single dirty module `DbContext`, flushes, drains domain events, and commits. Each module DB hosts its own Wolverine outbox tables (ancillary store) so envelopes commit with business data; `messagingdb` is kept as the Wolverine main store holding only shared infra (inbox, local queues, scheduled, dead-letter). Startup provisions ancillary stores and serializes migrations with a Postgres advisory lock.

**Tech Stack:** .NET 10 / C# 13, EF Core (Npgsql), Ardalis.Specification 9.3.1, WolverineFx 5.37.2 (+ EntityFrameworkCore, Postgresql), Mediator 3.0.2, xUnit + Testcontainers.PostgreSql, Aspire AppHost.

## Global Constraints

- `TreatWarningsAsErrors=true` — build must be 0 warnings. (`Directory.Build.props`)
- Package versions live in `Directory.Packages.props` only — never pin in a `.csproj`.
- File-scoped namespaces; private fields `_camelCase`; constants `PascalCase`.
- One module's `DbContext` per command (enforced by `TransactionBehavior`).
- Integration events only published inside an `ITransactional` command, via `IIntegrationEventPublisher`.
- Local user `Guid` is canonical; Keycloak external id only at Keycloak/JWT/SignalR boundaries.
- No secrets in source; design-time factories read `EF_DESIGN_*`.
- No AI co-author trailer on commits.
- Definition of Done per task: relevant tests pass AND `dotnet build AllSpice.CleanModularMonolith.slnx` is clean.
- **No primary-constructor conversions in this phase** (that is the isolated Phase 4 PR).

---

## File structure (Phase 0)

| File | Responsibility | Action |
|---|---|---|
| `Shared/.../SharedKernel/Repositories/EfRepository.cs` | Track-only repository base (no auto-flush) | Modify |
| `Services/.../Identity/Infrastructure/Repositories/UserRepository.cs` | Repoint base to `EfRepository` | Modify |
| `Services/.../Identity/Infrastructure/Repositories/InvitationRepository.cs` | Repoint base | Modify |
| `Services/.../Notifications/Infrastructure/Repositories/NotificationRepository.cs` | Repoint base | Modify |
| `Services/.../Notifications/Infrastructure/Repositories/NotificationTemplateRepository.cs` | Repoint base | Modify |
| `Services/.../Notifications/Infrastructure/Repositories/NotificationPreferenceRepository.cs` | Repoint base | Modify |
| `Shared/.../SharedKernel/Behaviors/TransactionBehavior.cs` | UoW: txn on dirty context, single flush+commit | Modify |
| `AllSpice.../ApiGateway/Infrastructure/Messaging/WolverineIntegrationEventPublisher.cs` | Enroll the single dirty context | Modify |
| `Services/.../Identity/Infrastructure/Persistence/IdentityDbContext.cs` | `MapWolverineEnvelopeStorage` | Modify |
| `Services/.../Notifications/Infrastructure/Persistence/NotificationsDbContext.cs` | `MapWolverineEnvelopeStorage` | Modify |
| `Services/.../Identity/Infrastructure/Extensions/IdentityModuleExtensions.cs` | Wolverine+interceptor registration | Modify |
| `Services/.../Notifications/Infrastructure/Extensions/NotificationsModuleExtensions.cs` | Wolverine+interceptor registration | Modify |
| `AllSpice.../ApiGateway/Extensions/GatewayModuleRegistrationExtensions.cs` | main + ancillary store wiring | Modify |
| `AllSpice.../ApiGateway/Program.cs` | provision ancillary stores at startup | Modify |
| `Shared/.../SharedKernel/Persistence/MigrationRunner.cs` | advisory-lock around migrate | Modify |
| `AllSpice.../AppHost/AppHost.cs` | keep messagingdb as infra store; ref both module DBs on gateway | Modify |
| `tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/` | Testcontainers harness + UoW + outbox + topology tests | Create |
| `AGENTS.md`, `ARCHITECTURE.md`, `TODOS.md`, publisher XML-doc | docs reflect atomicity, remove caveat | Modify |

---

## Task 1: Testcontainers Postgres harness

**Files:**
- Create: `tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests.csproj`
- Create: `tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/PostgresFixture.cs`
- Modify: `Directory.Packages.props` (add `Testcontainers.PostgreSql` if absent)
- Modify: `AllSpice.CleanModularMonolith.slnx` (add the project)

**Interfaces:**
- Produces: `PostgresFixture` implementing `IAsyncLifetime` exposing `string ConnectionString(string dbName)` that returns a connection string to a freshly-created database on the shared container; `Task<PostgreSqlContainer>` lifecycle.

- [ ] **Step 1: Add the Testcontainers package version**

In `Directory.Packages.props`, add inside `<ItemGroup>`:

```xml
<PackageVersion Include="Testcontainers.PostgreSql" Version="4.0.0" />
```

(If a newer version is already standard in the repo, match it. Verify with `dotnet list package --outdated` after restore.)

- [ ] **Step 2: Create the test project file**

`tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Testcontainers.PostgreSql" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Services\AllSpice.CleanModularMonolith.Identity\AllSpice.CleanModularMonolith.Identity.csproj" />
    <ProjectReference Include="..\..\Services\AllSpice.CleanModularMonolith.Notifications\AllSpice.CleanModularMonolith.Notifications.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.SharedKernel\AllSpice.CleanModularMonolith.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

(Match the exact `PackageReference` style other test `.csproj` files use — central versioning means no `Version=` here.)

- [ ] **Step 3: Create the Postgres fixture**

`tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/PostgresFixture.cs`:

```csharp
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task<string> CreateDatabaseAsync(string dbName)
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await cmd.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = dbName };
        return builder.ConnectionString;
    }

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
```

- [ ] **Step 4: Add the project to the solution and build**

Run:
```bash
dotnet sln AllSpice.CleanModularMonolith.slnx add tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests.csproj
dotnet build tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests.csproj
```
Expected: build succeeds (Docker not needed to build, only to run).

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props AllSpice.CleanModularMonolith.slnx tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/
git commit -m "test(foundation): add Testcontainers Postgres harness"
```

---

## Task 2: Failing test — Identity command must be atomic (proves the F1 bug)

This test registers BOTH modules (the condition that hides the bug) and asserts an Identity command rolls back fully on failure. It MUST fail on current code.

**Files:**
- Create: `tests/.../Foundation.IntegrationTests/UnitOfWorkAtomicityTests.cs`
- Create: `tests/.../Foundation.IntegrationTests/TwoModuleHost.cs` (builds a real DI scope with both module DbContexts + the pipeline)

**Interfaces:**
- Consumes: `PostgresFixture.CreateDatabaseAsync`.
- Produces: `TwoModuleHost` exposing `IServiceProvider Services`, with both `IdentityDbContext` and `NotificationsDbContext` registered as `IModuleDbContext` in Notifications-then-Identity order (mirrors `GatewayModuleRegistrationExtensions`), the mediator pipeline behaviors registered, and a `MediatorDomainEventDispatcher`.

- [ ] **Step 1: Write `TwoModuleHost`**

Build a DI container that mirrors the gateway's module registration order (Notifications first, Identity second) and the pipeline behaviors, pointing each context at its own Testcontainers DB. Include `AddSharedKernelInterceptors`, `MediatorDomainEventDispatcher`, both repositories, and the five pipeline behaviors in the documented order.

```csharp
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Interceptors;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

public sealed class TwoModuleHost
{
    public IServiceProvider Services { get; }

    private TwoModuleHost(IServiceProvider services) => Services = services;

    public static async Task<TwoModuleHost> CreateAsync(PostgresFixture pg)
    {
        var notificationsCs = await pg.CreateDatabaseAsync("notificationsdb_" + Guid.NewGuid().ToString("N"));
        var identityCs = await pg.CreateDatabaseAsync("identitydb_" + Guid.NewGuid().ToString("N"));

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton<ICurrentUserProvider, TestCurrentUserProvider>();
        services.AddSharedKernelInterceptors();
        services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

        // Notifications FIRST, Identity SECOND — mirrors gateway registration order
        services.AddDbContext<NotificationsDbContext>((sp, o) =>
            o.UseNpgsql(notificationsCs).AddInterceptors(sp.GetServices<IInterceptor>()));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

        services.AddDbContext<IdentityDbContext>((sp, o) =>
            o.UseNpgsql(identityCs).AddInterceptors(sp.GetServices<IInterceptor>()));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Mediator + behaviors in documented order
        services.AddMediator();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(DomainExceptionBehavior<,>));

        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
        }
        return new TwoModuleHost(provider);
    }
}

internal sealed class TestCurrentUserProvider : ICurrentUserProvider
{
    public string? UserId => "00000000-0000-0000-0000-000000000001";
}
```

> NOTE: confirm the exact `ICurrentUserProvider` member names against `Shared/.../SharedKernel/Identity/ICurrentUserProvider.cs` and adjust `TestCurrentUserProvider`. Also confirm `AddMediator()` is the correct registration call used in the modules' `MediatorConfiguration`.

- [ ] **Step 2: Write the failing atomicity test**

`UnitOfWorkAtomicityTests.cs`: persist a `User`, then force the second write to fail inside the same command, and assert NO user row remains. On current code the user row is auto-committed by `AddAsync`, so it WILL remain → test fails.

```csharp
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class UnitOfWorkAtomicityTests(PostgresFixture pg)
{
    [Fact]
    public async Task Failed_command_rolls_back_all_writes()
    {
        var host = await TwoModuleHost.CreateAsync(pg);
        await using var scope = host.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<Mediator.IMediator>();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            mediator.Send(new TwoWriteThenThrowCommand()).AsTask());

        // Fresh scope to read committed state
        await using var verify = host.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var count = await db.Users.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, count); // FAILS on current code: the first AddAsync auto-committed
    }
}
```

Add a tiny test-only command + handler that writes one `User` via the repository then throws, marked `ITransactional`. Put it in the test project:

```csharp
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Mediator;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

public sealed record TwoWriteThenThrowCommand : IRequest<Result>, ITransactional;

public sealed class TwoWriteThenThrowCommandHandler(IUserRepository users)
    : IRequestHandler<TwoWriteThenThrowCommand, Result>
{
    public async ValueTask<Result> Handle(TwoWriteThenThrowCommand request, CancellationToken ct)
    {
        var user = User.Create(ExternalUserId.From(Guid.NewGuid().ToString()),
            "a@b.com", "a@b.com", "A", "B");
        await users.AddAsync(user, ct);
        throw new InvalidOperationException("boom after first write");
    }
}
```

> NOTE: confirm `User.Create` signature and `IRequest`/`ITransactional` against the real types; adjust the constructed args. This handler uses a primary constructor — allowed *in test code*; the production no-sweep rule is about shipping code.

- [ ] **Step 3: Run and confirm it FAILS**

Run (Docker must be running):
```bash
dotnet test tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests --filter "FullyQualifiedName~Failed_command_rolls_back_all_writes"
```
Expected: FAIL — asserts 0 users but finds 1 (the bug). If it errors on registration, fix `TwoModuleHost` until the test runs and fails on the assertion, not on setup.

- [ ] **Step 4: Commit the failing test**

```bash
git add tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/
git commit -m "test(foundation): failing two-module atomicity test (reproduces UoW bug)"
```

---

## Task 3: Make repositories track-only via `EfRepository`

**Files:**
- Modify: `Shared/.../SharedKernel/Repositories/EfRepository.cs`
- Modify: `Services/.../Identity/Infrastructure/Repositories/UserRepository.cs`
- Modify: `Services/.../Identity/Infrastructure/Repositories/InvitationRepository.cs`
- Modify: `Services/.../Notifications/Infrastructure/Repositories/NotificationRepository.cs`
- Modify: `Services/.../Notifications/Infrastructure/Repositories/NotificationTemplateRepository.cs`
- Modify: `Services/.../Notifications/Infrastructure/Repositories/NotificationPreferenceRepository.cs`

**Interfaces:**
- Produces: `EfRepository<TContext,TAggregate>` whose `Add/Update/Delete` stage only (override `SaveChangesAsync` to a no-op). Bespoke repos extend it and keep a typed `_dbContext` for custom queries.

- [ ] **Step 1: Override `SaveChangesAsync` to no-op in `EfRepository`**

Append inside the `EfRepository` class body:

```csharp
    /// <summary>
    /// Track-only: write methods (Add/Update/Delete) stage entities in the change tracker but do NOT
    /// flush. The unit-of-work boundary (the single SaveChanges + Commit) is owned by TransactionBehavior,
    /// which calls SaveChanges on the DbContext directly. This makes ITransactional commands atomic:
    /// nothing is persisted until the behavior commits, so any failure rolls back every write together.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
```

> NOTE: confirm `SaveChangesAsync` is `public virtual` on Ardalis `RepositoryBase<T>` 9.3.1 (it is). If the access modifier differs, match it.

- [ ] **Step 2: Repoint `UserRepository`**

Change the class declaration and keep the existing custom query methods unchanged:

```csharp
public sealed class UserRepository : EfRepository<IdentityDbContext, User>, IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }
    // ... existing GetByExternalIdAsync / GetByEmailAsync / etc. UNCHANGED ...
}
```

Add `using AllSpice.CleanModularMonolith.SharedKernel.Repositories;` and remove `using Ardalis.Specification.EntityFrameworkCore;` if now unused (warnings-as-errors).

- [ ] **Step 3: Repoint the other four repositories the same way**

`InvitationRepository` → `EfRepository<IdentityDbContext, Invitation>`; `NotificationRepository` → `EfRepository<NotificationsDbContext, Notification>`; `NotificationTemplateRepository` → `EfRepository<NotificationsDbContext, NotificationTemplate>`; `NotificationPreferenceRepository` → `EfRepository<NotificationsDbContext, NotificationPreference>`. Preserve each repo's custom methods and its `_dbContext` field. `NotificationRepository` currently has no custom members — it becomes:

```csharp
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationRepository(NotificationsDbContext dbContext)
    : EfRepository<NotificationsDbContext, Notification>(dbContext), INotificationRepository;
```

> Exception to the no-primary-ctor rule is NOT taken here — write the classic ctor form to match the phase rule. Use:
```csharp
public sealed class NotificationRepository : EfRepository<NotificationsDbContext, Notification>, INotificationRepository
{
    public NotificationRepository(NotificationsDbContext dbContext) : base(dbContext) { }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx`
Expected: clean (0 warnings). Fix any unused-using errors.

- [ ] **Step 5: Run the existing repository integration tests**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests`
Expected: these tests call repo methods then read back. Some assume `AddAsync` persisted immediately. **Any failure here is expected and informative** — those tests must be updated to save via the context or to assert staging. Update them to call `dbContext.SaveChangesAsync()` after repo `AddAsync` where they previously relied on auto-save. Re-run until green.

- [ ] **Step 6: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.SharedKernel/Repositories/EfRepository.cs Services/ tests/AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests/
git commit -m "refactor(persistence): repositories stage only; EfRepository owns track-only writes"
```

---

## Task 4: Rewrite `TransactionBehavior` to own the unit of work

**Files:**
- Modify: `Shared/.../SharedKernel/Behaviors/TransactionBehavior.cs`

**Interfaces:**
- Consumes: `IEnumerable<IModuleDbContext>`, `IDomainEventDispatcher`. Produces: same public shape (a pipeline behavior); behavior change only.

- [ ] **Step 1: Replace the `Handle` body**

Open the transaction AFTER the handler, on the single dirty context. Keep the multi-dirty guard. Drain domain events inside the transaction.

```csharp
    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Repositories stage only (see EfRepository), so the handler performs no DB writes itself.
        var response = await next(request, cancellationToken).ConfigureAwait(false);

        // Exactly one module DbContext may be dirty. Cross-module side effects go through integration events.
        var dirty = _dbContexts.Where(c => c.Instance.ChangeTracker.HasChanges()).ToList();
        if (dirty.Count == 0)
        {
            return response; // nothing to persist — no transaction needed
        }

        if (dirty.Count > 1)
        {
            var names = string.Join(", ", dirty.Select(c => c.Instance.GetType().Name));
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} mutated multiple module DbContexts ({names}). " +
                "A command must touch only one module. Cross-module side effects must be published as " +
                "integration events through IIntegrationEventPublisher.");
        }

        var db = dirty[0].Instance;
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Began transaction {TransactionId} for {RequestType}", transaction.TransactionId, typeof(TRequest).Name);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // flush staged writes in the txn

            // Drain domain events (handlers may stage more, and publish integration events that enroll this txn).
            bool hasMore = true;
            while (hasMore)
            {
                var events = db.ChangeTracker.Entries<IHasDomainEvents>()
                    .SelectMany(e => e.Entity.TakeDomainEvents())
                    .ToList();
                if (events.Count == 0)
                {
                    hasMore = false;
                    continue;
                }
                _logger.LogDebug("Dispatching {Count} domain events", events.Count);
                await _dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Committed transaction {TransactionId} for {RequestType}", transaction.TransactionId, typeof(TRequest).Name);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Rolled back transaction {TransactionId} for {RequestType}", transaction.TransactionId, typeof(TRequest).Name);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
```

Update the class XML-doc to describe the new order (handler stages → flush → drain → commit). Remove the old pre-handler-begin comment block.

- [ ] **Step 2: Note the behavioral change for direct publishes**

Add a one-line code comment near the drain loop: integration events are published by domain-event handlers, which run here inside the open transaction, so `WolverineIntegrationEventPublisher`'s "must have an active transaction" guard is satisfied. (A handler publishing an integration event directly in its body — before drain — would now correctly throw; this codebase publishes only via domain events.)

- [ ] **Step 3: Build**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx`
Expected: clean.

- [ ] **Step 4: Run the failing atomicity test — it must now PASS**

Run:
```bash
dotnet test tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests --filter "FullyQualifiedName~Failed_command_rolls_back_all_writes"
```
Expected: PASS (0 users after rollback).

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test AllSpice.CleanModularMonolith.slnx`
Expected: all green. Investigate any module test that assumed pre-handler transaction timing.

- [ ] **Step 6: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.SharedKernel/Behaviors/TransactionBehavior.cs
git commit -m "fix(persistence): TransactionBehavior owns the unit of work on the single dirty context"
```

---

## Task 5: Publisher enrolls the single dirty context (explicit)

**Files:**
- Modify: `AllSpice.../ApiGateway/Infrastructure/Messaging/WolverineIntegrationEventPublisher.cs`

**Interfaces:** unchanged public shape.

- [ ] **Step 1: Tighten the context selection**

It currently picks `FirstOrDefault(c => CurrentTransaction is not null)`. With Task 4 only the dirty context has a transaction, so this is already correct — but make intent explicit and keep the guard. Confirm the existing code still reads:

```csharp
var transactionalContext = _dbContexts
    .FirstOrDefault(c => c.Instance.Database.CurrentTransaction is not null);
if (transactionalContext is null)
{
    throw new InvalidOperationException(
        $"Cannot publish {typeof(T).Name}: no active DbContext transaction. " +
        "Integration events must be published from inside an ITransactional command.");
}
_outbox.Enroll(transactionalContext.Instance);
await _outbox.PublishAsync(message);
```

Update the XML-doc: remove the "shared messagingdb is NOT atomic" caveat; state that envelopes are co-located in the module DB and commit atomically with the business data.

- [ ] **Step 2: Build + commit**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` (clean), then:
```bash
git add AllSpice.CleanModularMonolith.ApiGateway/Infrastructure/Messaging/WolverineIntegrationEventPublisher.cs
git commit -m "docs(messaging): publisher enrolls the dirty module context; envelopes now atomic"
```

---

## Task 6: VERIFY then wire the hybrid outbox (DbContexts + module registration)

This task carries the one real integration unknown: reconciling Wolverine's EF integration with the SP-aware interceptor attachment that PR #4 added. Resolve it before wiring both modules.

**Files:**
- Modify: `IdentityDbContext.cs`, `NotificationsDbContext.cs` (`MapWolverineEnvelopeStorage`)
- Modify: `IdentityModuleExtensions.cs`, `NotificationsModuleExtensions.cs` (registration)

**Interfaces:**
- Produces: each module `DbContext` model contains Wolverine envelope tables; each registered so EF interceptors (`sp.GetServices<IInterceptor>()`) AND Wolverine outbox integration both apply.

- [ ] **Step 1: VERIFY the registration approach (spike)**

The reverted `8ab369b` used `AddDbContextWithWolverineIntegration<T>(o => o.UseNpgsql(cs))` — which dropped `AddInterceptors(sp.GetServices<IInterceptor>())` and `EnrichNpgsqlDbContext`. Confirm which of these holds (write a 5-line throwaway in the test project or check Wolverine 5.37 source/docs):
  - (a) `AddDbContextWithWolverineIntegration<T>` has an overload taking `(IServiceProvider, DbContextOptionsBuilder)` → use it and call `.AddInterceptors(sp.GetServices<IInterceptor>())` inside.
  - (b) It does not → register the context with the existing `AddDbContextPool<T>((sp,o) => o.UseNpgsql(cs).AddInterceptors(sp.GetServices<IInterceptor>()))`, keep `MapWolverineEnvelopeStorage` in the model, and rely on `PersistMessagesWithPostgresql(..., Ancillary).Enroll<T>()` + `UseEntityFrameworkCoreTransactions()` for the outbox (i.e., the special Add method may be optional when the model maps envelope storage and the store is enrolled).
  - Record the finding as a comment in the module extension file.

Expected output of this step: a definitive note "use overload X" written into the PR. Do not proceed until known.

- [ ] **Step 2: Map envelope storage in both DbContexts**

In `IdentityDbContext.OnModelCreating`, after `ApplySoftDeleteFilters()`:

```csharp
using Wolverine.EntityFrameworkCore; // add to usings
// ...
        // Co-locate the Wolverine durable outbox tables in this module's own database so integration
        // events commit in the SAME transaction as the business data.
        modelBuilder.MapWolverineEnvelopeStorage("wolverine");
```

Do the identical change in `NotificationsDbContext.OnModelCreating`.

- [ ] **Step 3: Apply the verified registration in both module extensions**

Using the approach confirmed in Step 1, update `AddIdentityModuleServices` and `AddNotificationsModuleServices` so BOTH the shared interceptors and Wolverine integration apply. Document the choice inline. Keep `AddScoped<IModuleDbContext>(...)` as-is.

- [ ] **Step 4: Build**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx`
Expected: clean.

- [ ] **Step 5: Commit**

```bash
git add Services/
git commit -m "feat(messaging): co-locate Wolverine outbox tables per module DbContext"
```

---

## Task 7: Gateway main+ancillary stores, startup provisioning, AppHost

**Files:**
- Modify: `GatewayModuleRegistrationExtensions.cs`
- Modify: `Program.cs`
- Modify: `AppHost.cs`

**Interfaces:**
- Consumes: connection strings `identitydb`, `notificationsdb`, `messagingdb`.
- Produces: `messagingdb` is the Wolverine main store (infra only); each module DB is an ancillary store holding its own envelopes; ancillary schemas migrated at startup.

- [ ] **Step 1: Configure stores in the gateway**

In `RegisterGatewayModules`, replace the messaging-store block. Keep `messagingdb` as the MAIN store; add each module DB as ancillary + enroll.

```csharp
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Wolverine.Persistence.Durability; // MessageStoreRole
// ...
var messagingCs = builder.Configuration.GetConnectionString("messagingdb");
var identityCs = builder.Configuration.GetConnectionString("identitydb");
var notificationsCs = builder.Configuration.GetConnectionString("notificationsdb");
if (string.IsNullOrWhiteSpace(messagingCs) || string.IsNullOrWhiteSpace(identityCs) || string.IsNullOrWhiteSpace(notificationsCs))
{
    throw new InvalidOperationException(
        "Connection strings 'messagingdb', 'identitydb' and 'notificationsdb' are required. messagingdb holds " +
        "shared Wolverine infrastructure (inbox/queues/scheduled/DLQ); each module DB hosts its own co-located outbox.");
}
// inside UseWolverine(opts => { ... }) replace the single PersistMessagesWithPostgresql with:
opts.PersistMessagesWithPostgresql(messagingCs, "wolverine"); // MAIN store: shared infra only
opts.PersistMessagesWithPostgresql(identityCs, "wolverine", MessageStoreRole.Ancillary).Enroll<IdentityDbContext>();
opts.PersistMessagesWithPostgresql(notificationsCs, "wolverine", MessageStoreRole.Ancillary).Enroll<NotificationsDbContext>();
```

Keep the existing durable-queue policies and typed retry config unchanged.

- [ ] **Step 2: Provision ancillary stores at startup**

In `Program.cs`, after the two `Ensure...DatabaseAsync` calls and before `UseGatewayPipeline`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Persistence.Durability;
// ...
  // Wolverine auto-builds the main store; ancillary module stores must be migrated explicitly.
  foreach (var messageStore in app.Services.GetServices<IMessageStore>())
  {
      await messageStore.Admin.MigrateAsync();
  }
```

- [ ] **Step 3: AppHost — keep messagingdb, ensure gateway references all three**

In `AppHost.cs`, confirm `messagingdb` resource still exists and the gateway project references `identitydb`, `notificationsdb`, AND `messagingdb`. (The current main branch already has messagingdb; this task only ensures all three references are present on the gateway and adds a comment that messagingdb is infra-only now.) Adjust only if a reference is missing.

- [ ] **Step 4: Build**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx`
Expected: clean.

- [ ] **Step 5: Commit**

```bash
git add AllSpice.CleanModularMonolith.ApiGateway/ AllSpice.CleanModularMonolith.AppHost/AppHost.cs
git commit -m "feat(messaging): hybrid outbox — messagingdb infra-only main store, per-module ancillary outbox"
```

---

## Task 8: Outbox atomicity + topology tests

**Files:**
- Create: `tests/.../Foundation.IntegrationTests/OutboxAtomicityTests.cs`
- Create: `tests/.../Foundation.IntegrationTests/OutboxTopologyTests.cs`

Use `git show 8ab369b -- '*RealOutboxWiringTests*'` as a reference for wiring the real DbContexts + Wolverine config against Testcontainers.

**Interfaces:**
- Consumes: `PostgresFixture`, the real module DbContexts, the real Wolverine config.

- [ ] **Step 1: Write the atomicity test (resurrect RealOutboxWiringTests)**

Assert: an integration event published inside an Identity command transaction is delivered AND the business row is persisted; a rolled-back transaction delivers nothing and persists no row. Port the logic from `8ab369b`'s `RealOutboxWiringTests` / `TransactionalOutboxTests`, adapting the store wiring to the hybrid topology (messagingdb main + module ancillary).

- [ ] **Step 2: Write the topology placement test (new)**

Assert: after an Identity command publishes an event, the envelope row exists in `identitydb`'s `wolverine` schema, and `messagingdb`'s envelope tables contain no business-module outbox envelope (only infra). Query the `wolverine.outgoing_envelopes` table in each DB directly via Npgsql.

```csharp
// pseudo-shape: after committing an Identity command that publishes an event,
// count rows in identitydb.wolverine outgoing envelopes -> >= 1
// count business-event rows in messagingdb -> 0
```

> Confirm the exact Wolverine table/schema names (`wolverine.wolverine_outgoing_envelopes` or similar) by inspecting a provisioned DB in the spike (Task 6 Step 1) and hard-code the verified name.

- [ ] **Step 3: Run and confirm PASS**

Run:
```bash
dotnet test tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests
```
Expected: all foundation tests green (Docker running).

- [ ] **Step 4: Commit**

```bash
git add tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/
git commit -m "test(messaging): outbox atomicity + co-location topology (real Postgres)"
```

---

## Task 9: Serialize migrations with a Postgres advisory lock (TODOS P1)

**Files:**
- Modify: `Shared/.../SharedKernel/Persistence/MigrationRunner.cs`
- (Provider guard so SQLite test DBs are unaffected.)

**Interfaces:**
- Produces: `MigrationRunner.RunForModuleAsync<TContext>` wraps `MigrateAsync` in `pg_advisory_lock(<stable-hash>)` on a dedicated Npgsql connection when the provider is Npgsql.

- [ ] **Step 1: Write a test for lock serialization**

In the Foundation test project, run two concurrent `RunForModuleAsync` calls against the same fresh DB and assert no exception and migrations applied once. (If `RunForModuleAsync` is hard to invoke directly, test the new `AdvisoryLock` helper in isolation: two tasks acquiring the same lock key serialize.)

- [ ] **Step 2: Implement the advisory lock**

Add a guarded advisory lock around the migrate call. Only for Npgsql provider; derive a stable `long` key from the context name.

```csharp
// inside the migrate path, when provider is Npgsql:
// var key = StableHash(typeof(TContext).FullName!);
// await using var conn = new NpgsqlConnection(cs); await conn.OpenAsync(ct);
// await using (var cmd = conn.CreateCommand()) { cmd.CommandText = "SELECT pg_advisory_lock(@k)"; cmd.Parameters.AddWithValue("k", key); await cmd.ExecuteNonQueryAsync(ct); }
// try { await db.Database.MigrateAsync(ct); }
// finally { await using var unlock = conn.CreateCommand(); unlock.CommandText = "SELECT pg_advisory_unlock(@k)"; unlock.Parameters.AddWithValue("k", key); await unlock.ExecuteNonQueryAsync(ct); }
```

> Keep SharedKernel provider-agnostic: detect Npgsql via `db.Database.IsNpgsql()` (the `Microsoft.EntityFrameworkCore` extension is already referenced) and fall back to the current direct `MigrateAsync` for SQLite.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests --filter "FullyQualifiedName~Advisory"`
Expected: PASS.

- [ ] **Step 4: Build full + commit (isolated commit per eng-review decision 6)**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` (clean), then:
```bash
git add Shared/AllSpice.CleanModularMonolith.SharedKernel/Persistence/MigrationRunner.cs tests/
git commit -m "fix(persistence): serialize startup migrations with a Postgres advisory lock"
```

---

## Task 10: Documentation — reflect atomicity, remove the caveat

**Files:**
- Modify: `AGENTS.md` (§6 outbox note), `ARCHITECTURE.md` (outbox/persistence sections), `TODOS.md` (close bundled items), `CLAUDE.md` if it references messagingdb.

- [ ] **Step 1: Update AGENTS.md and ARCHITECTURE.md**

State the new model: repositories stage; `TransactionBehavior` owns the single flush+commit on the dirty module context; integration-event envelopes are co-located per module and commit atomically with business data; `messagingdb` is the Wolverine main store holding shared infra only. Remove any "cross-DB non-atomic / accepted trade-off" language.

- [ ] **Step 2: Close TODOS items**

Mark resolved in `TODOS.md`: the cross-DB atomicity limitation and the migration advisory-lock P1. Leave deferred items (prompt outbox flush, AddNpgsqlDbContext enrichment, AzureBlob SAS) as-is.

- [ ] **Step 3: Final full build + test**

Run:
```bash
dotnet build AllSpice.CleanModularMonolith.slnx
dotnet test AllSpice.CleanModularMonolith.slnx
```
Expected: build clean (0 warnings), all tests green (Docker running for integration tests).

- [ ] **Step 4: Commit**

```bash
git add AGENTS.md ARCHITECTURE.md TODOS.md CLAUDE.md
git commit -m "docs: unit-of-work + hybrid transactional outbox now atomic; close bundled TODOs"
```

---

## Phase 0 Definition of Done
- `Failed_command_rolls_back_all_writes` passes (the original UoW bug is fixed and regression-locked).
- Outbox atomicity + topology tests pass against real Postgres.
- Migration advisory-lock test passes.
- `dotnet build` clean (warnings-as-errors); `dotnet test` green.
- Docs reflect atomicity; cross-DB caveat removed; bundled TODOs closed.
- This is its own PR; Phases 1-4 stack on top after it merges.

## Self-review notes
- **Spec coverage:** F1 (Tasks 3-4), outbox hybrid (Tasks 6-7), publisher (Task 5), migration lock (Task 9), foundation tests incl. two-module + topology (Tasks 2, 8), docs (Task 10). Covered.
- **Known unknowns flagged, not hidden:** Wolverine+interceptor reconciliation (Task 6 Step 1), exact Wolverine table names (Task 8 Step 2), Ardalis `SaveChangesAsync` visibility (Task 3 Step 1), `User.Create`/`ICurrentUserProvider` signatures (Task 2). Each has a verify-then-proceed step.
- **Type consistency:** `EfRepository<TContext,TAggregate>` used identically in Tasks 3/8; `IModuleDbContext.Instance` used in Task 4 matches existing interface.
</content>
