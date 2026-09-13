# Opt-in Event Sourcing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-aggregate, opt-in event sourcing (Marten) to the template — enlisted in the module's existing EF Core transaction and Wolverine outbox — with a reference `Ledger` module, guard-rail tests, and documentation.

**Architecture:** The module `DbContext` stays the transaction owner. A scoped `MartenTransactionParticipant` opens a Marten session inside that transaction (`SessionOptions.ForTransaction(tx, shouldAutoCommit: false)`), beginning the transaction early if the handler needs a session before commit. `TransactionBehavior` learns about `ITransactionParticipant`s: they count toward the one-module-per-command rule, are flushed in each drain-loop iteration, contribute domain events, and are discarded on failure. Domain layers never reference Marten; a new shared `EventSourcing` project holds all Marten code.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Marten 9.33.0, WolverineFx 6.36.0, Mediator (source-gen), FastEndpoints, Ardalis (Result/GuardClauses/SmartEnum), xUnit + Moq, Testcontainers.PostgreSql, NetArchTest.

**Spec:** `docs/superpowers/specs/2026-09-13-opt-in-event-sourcing-design.md`

## Global Constraints

- `TreatWarningsAsErrors=true` — every task ends with `dotnet build AllSpice.CleanModularMonolith.slnx` producing **0 warnings**.
- Package versions live **only** in `Directory.Packages.props` (central package management). Never pin a version in a `.csproj`.
- Exact versions: `Marten` **9.33.0**; `WolverineFx`, `WolverineFx.RuntimeCompilation`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql` all **6.36.0**. `WolverineFx.Marten` is **not** added.
- Marten may be referenced only by `Shared/AllSpice.CleanModularMonolith.EventSourcing` and by `*.Infrastructure*` namespaces of modules (enforced by an architecture test in Task 13). Domain and Application layers never `using Marten`.
- Marten store settings fixed by `AddModuleEventStore`: `AutoCreateSchemaObjects = AutoCreate.None`, `EventAppendMode.Rich`, System.Text.Json, correlation-id + header metadata enabled, Guid stream identity.
- Event-sourced aggregates use **Guid** stream ids (`EventSourcedAggregate : Entity<Guid>`). Stream events are `public sealed record … : IDomainEvent` carrying ids, amounts, timestamps, references — **never personal data**.
- The `When(IDomainEvent)` dispatcher on aggregates must **not** be named `Apply` (Marten's convention scanner also binds interface-typed `Apply` overloads → double application).
- Code conventions (ARCHITECTURE.md / AGENTS.md §7): file-scoped namespaces, `_camelCase` private fields, primary constructors for DI'd services with `private readonly` field copies, XML doc comments on public types explaining *why*.
- Commit messages: conventional (`feat(scope): …`), **no AI co-author trailer** of any kind. Work on branch `feat/opt-in-event-sourcing` (already created; the spec is committed there).
- Tests use xUnit + Moq; test projects named `{Module}.{Layer}.UnitTests` / `IntegrationTests`. Integration tests that need Postgres use `Testcontainers.PostgreSql` with image `postgres:16-alpine` (Docker required locally and available in CI).
- Definition of Done per task: build 0 warnings, the task's tests pass, `git commit`.

---

## File structure

**Modified**
- `Directory.Packages.props` — version bumps + Marten.
- `Shared/AllSpice.CleanModularMonolith.SharedKernel/Behaviors/TransactionBehavior.cs` — participant-aware unit of work.
- `Shared/AllSpice.CleanModularMonolith.SharedKernel/Results/DomainExceptionResultMapper.cs` — `ConcurrencyConflictException` → `Result.Conflict`.
- `AllSpice.CleanModularMonolith.ApiGateway/Middleware/ErrorHandlingMiddleware.cs` — 409 mapping.
- `AllSpice.CleanModularMonolith.ApiGateway/Program.cs` — JasperFx command runner, Ledger `Ensure`.
- `AllSpice.CleanModularMonolith.ApiGateway/Extensions/GatewayModuleRegistrationExtensions.cs` — Ledger registration, ancillary store, metadata provider.
- `AllSpice.CleanModularMonolith.ApiGateway/Extensions/GatewayServiceCollectionExtensions.cs` — Ledger API assembly.
- `AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.http` — Ledger requests.
- `AllSpice.CleanModularMonolith.AppHost/AppHost.cs` — `ledgerdb`.
- `AllSpice.CleanModularMonolith.slnx` — new projects.
- `tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Behaviors/*` — constructor change + new tests.
- `tests/AllSpice.CleanModularMonolith.Architecture.Tests/*` — Ledger + Marten rules.
- `tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/*` — event-sourced outbox atomicity.
- Docs: `README.md`, `ARCHITECTURE.md`, `AGENTS.md`, `GETTING_STARTED.md`, `CLAUDE.md`, `TODOS.md`, `deploy/README.md`, `.template.config/template.json`, `.claude/skills/allspice-clean-review/checklist.md`, `docs/adr/README.md`.

**Created — SharedKernel** (`Shared/AllSpice.CleanModularMonolith.SharedKernel/`)
- `Exceptions/ConcurrencyConflictException.cs`
- `Persistence/ITransactionParticipant.cs`
- `EventSourcing/IEventSourcedAggregate.cs`, `EventSourcing/EventSourcedAggregate.cs`, `EventSourcing/IEventSourcedRepository.cs`, `EventSourcing/StoredEvent.cs`

**Created — EventSourcing project** (`Shared/AllSpice.CleanModularMonolith.EventSourcing/`)
- `AllSpice.CleanModularMonolith.EventSourcing.csproj`
- `IEventMetadataProvider.cs`, `NullEventMetadataProvider.cs`
- `IModuleEventStoreSession.cs`
- `MartenTransactionParticipant.cs`
- `MartenEventSourcedRepository.cs`
- `ModuleEventStoreExtensions.cs`

**Created — Ledger module** (`Services/AllSpice.CleanModularMonolith.Ledger/`)
- `AllSpice.CleanModularMonolith.Ledger.csproj`, `GlobalUsings.cs`, `MediatorConfiguration.cs`
- `Domain/ValueObjects/Currency.cs`, `Domain/ValueObjects/Money.cs`, `Domain/Enums/AccountStatus.cs`
- `Domain/Events/AccountOpened.cs`, `FundsDeposited.cs`, `FundsWithdrawn.cs`, `AccountClosed.cs`, `Legacy/FundsDepositedV1.cs`, `Legacy/FundsDepositedUpcasts.cs`
- `Domain/Exceptions/InsufficientFundsException.cs`
- `Domain/Aggregates/Account.cs`
- `Application/AssemblyReference.cs`, `Application/Contracts/Persistence/IAccountRepository.cs`, `Application/DTOs/AccountSummaryDto.cs`, `Application/DTOs/AccountHistoryEntryDto.cs`
- `Application/Features/Accounts/Commands/{OpenAccount,DepositFunds,WithdrawFunds,CloseAccount}/*` (Command, Handler, Validator each)
- `Application/Features/Accounts/Queries/{GetAccountSummary,GetAccountHistory}/*`
- `Application/Features/Accounts/Events/AccountOpenedDomainEventHandler.cs`
- `Infrastructure/Persistence/ILedgerEventStore.cs`, `LedgerDbContext.cs`, `LedgerDbContextDesignTimeFactory.cs`, `Migrations/*` (generated)
- `Infrastructure/Projections/AccountSummary.cs`, `Infrastructure/Projections/AccountSummaryProjection.cs`
- `Infrastructure/Repositories/AccountRepository.cs`
- `Infrastructure/Authorization/LedgerPermissionManifest.cs`
- `Infrastructure/Extensions/LedgerModuleExtensions.cs`
- `Api/Endpoints/Accounts/*` (6 endpoints + 2 request types)
- `Shared/AllSpice.CleanModularMonolith.ApiContracts/Ledger/Responses/*` (3 records)
- `AllSpice.CleanModularMonolith.ApiGateway/Infrastructure/EventSourcing/HttpEventMetadataProvider.cs`

**Created — tests**
- `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/` (Testcontainers) — participant + repository semantics with a probe aggregate.
- `tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests/`
- `tests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests/`
- `tests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests/` (Testcontainers)
- `docs/adr/0009-opt-in-event-sourcing-marten-enlisted.md`

---

### Task 1: Align Wolverine to 6.36.0 and add Marten 9.33.0

**Files:**
- Modify: `Directory.Packages.props`

**Interfaces:**
- Produces: package versions every later task relies on.

- [ ] **Step 1: Edit the versions**

In `Directory.Packages.props` change the four Wolverine lines and add Marten directly after them:

```xml
    <PackageVersion Include="WolverineFx" Version="6.36.0" />
    <PackageVersion Include="WolverineFx.RuntimeCompilation" Version="6.36.0" />
    <PackageVersion Include="WolverineFx.EntityFrameworkCore" Version="6.36.0" />
    <PackageVersion Include="WolverineFx.Postgresql" Version="6.36.0" />
    <!-- Event store for opt-in event-sourced aggregates (ADR-0009). Marten 9 needs JasperFx >= 2.67, which is
         why the Wolverine family above is kept at a version that shares that JasperFx line. WolverineFx.Marten
         is deliberately NOT referenced: the module DbContext owns the transaction and hosts the outbox; Marten
         enlists in that transaction (see Shared/*.EventSourcing). -->
    <PackageVersion Include="Marten" Version="9.33.0" />
```

- [ ] **Step 2: Restore, build and run the whole suite**

Run: `dotnet restore AllSpice.CleanModularMonolith.slnx && dotnet build AllSpice.CleanModularMonolith.slnx --no-restore && dotnet test AllSpice.CleanModularMonolith.slnx --no-build`
Expected: build succeeds with 0 warnings; all tests pass. If the Wolverine minor bump surfaces a compile error (renamed API), fix it in place and note it in the commit body — do **not** downgrade.

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "build(deps): align WolverineFx to 6.36.0 and add Marten 9.33.0"
```

---

### Task 2: `ConcurrencyConflictException` (409) across pipeline and middleware

**Files:**
- Create: `Shared/AllSpice.CleanModularMonolith.SharedKernel/Exceptions/ConcurrencyConflictException.cs`
- Modify: `Shared/AllSpice.CleanModularMonolith.SharedKernel/Results/DomainExceptionResultMapper.cs`
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/Middleware/ErrorHandlingMiddleware.cs:65-83`
- Test: `tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Exceptions/ConcurrencyConflictExceptionTests.cs`

**Interfaces:**
- Produces: `ConcurrencyConflictException(string message)` and `ConcurrencyConflictException(string message, Exception inner)`; `Code == "concurrency_conflict"`; mapped to `Result.Conflict` / HTTP 409.

- [ ] **Step 1: Write the failing test**

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Results;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Exceptions;

public class ConcurrencyConflictExceptionTests
{
    [Fact]
    public void Code_is_concurrency_conflict()
    {
        var ex = new ConcurrencyConflictException("Account 1 was modified by another request.");
        Assert.Equal("concurrency_conflict", ex.Code);
    }

    [Fact]
    public void Maps_to_a_Conflict_result()
    {
        var result = DomainExceptionResultMapper.MapToResult<Result>(new ConcurrencyConflictException("stale"));
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("stale", result.Errors);
    }

    [Fact]
    public void Maps_to_a_typed_Conflict_result()
    {
        var result = DomainExceptionResultMapper.MapToResult<Result<Guid>>(new ConcurrencyConflictException("stale"));
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests --filter "FullyQualifiedName~ConcurrencyConflictExceptionTests"`
Expected: compile error — `ConcurrencyConflictException` does not exist.

- [ ] **Step 3: Create the exception**

```csharp
namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

/// <summary>
/// Thrown when a write lost an optimistic-concurrency race: the aggregate (or event stream) was modified by
/// another request between load and commit. Raised by the event-store participant (Marten
/// <c>ConcurrencyException</c>) and by <c>TransactionBehavior</c> for EF Core's
/// <c>DbUpdateConcurrencyException</c>, so both persistence styles surface the same 409 with the same
/// machine-readable <see cref="Code"/>. Clients should reload and retry.
/// </summary>
public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string Code => "concurrency_conflict";
}
```

- [ ] **Step 4: Map it in `DomainExceptionResultMapper`**

In both `MapToUntypedResult` and `MapToTypedResult<T>` add a case **above** the `ConflictException` case:

```csharp
            ConcurrencyConflictException ex => Result.Conflict(ex.Message),
```
and
```csharp
            ConcurrencyConflictException ex => Result<T>.Conflict(ex.Message),
```

- [ ] **Step 5: Map it in `ErrorHandlingMiddleware`**

In the `statusCode` switch add, directly after the `ConflictException` line:

```csharp
            ConcurrencyConflictException => HttpStatusCode.Conflict,
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests --filter "FullyQualifiedName~ConcurrencyConflictExceptionTests"`
Expected: 3 passed. Then `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.SharedKernel/Exceptions/ConcurrencyConflictException.cs Shared/AllSpice.CleanModularMonolith.SharedKernel/Results/DomainExceptionResultMapper.cs AllSpice.CleanModularMonolith.ApiGateway/Middleware/ErrorHandlingMiddleware.cs tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Exceptions/ConcurrencyConflictExceptionTests.cs
git commit -m "feat(sharedkernel): ConcurrencyConflictException mapped to 409 concurrency_conflict"
```

---

### Task 3: SharedKernel event-sourcing abstractions (no Marten)

**Files:**
- Create: `Shared/AllSpice.CleanModularMonolith.SharedKernel/Persistence/ITransactionParticipant.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.SharedKernel/EventSourcing/IEventSourcedAggregate.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.SharedKernel/EventSourcing/EventSourcedAggregate.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.SharedKernel/EventSourcing/StoredEvent.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.SharedKernel/EventSourcing/IEventSourcedRepository.cs`
- Test: `tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/EventSourcing/EventSourcedAggregateTests.cs`

**Interfaces:**
- Produces:
  - `ITransactionParticipant { DbContext Owner; bool HasPendingChanges; ValueTask FlushAsync(CancellationToken); IEnumerable<IDomainEvent> TakeDomainEvents(); void Discard(); }`
  - `IEventSourcedAggregate : IHasDomainEvents { Guid Id; long Version; IReadOnlyList<IDomainEvent> UncommittedEvents; bool IsMarkedForArchive; void SetVersion(long); void ClearUncommittedEvents(); }`
  - `abstract class EventSourcedAggregate : Entity<Guid>, IAggregateRoot, IEventSourcedAggregate` with `protected void Raise(IDomainEvent)`, `protected abstract void When(IDomainEvent)`, `protected void MarkForArchive()`.
  - `sealed record StoredEvent(long Version, long Sequence, DateTimeOffset Timestamp, string EventType, string? CorrelationId, IReadOnlyDictionary<string, object?> Headers, IDomainEvent Data)`
  - `IEventSourcedRepository<TAggregate> { Task<TAggregate?> LoadAsync(Guid, CancellationToken); Task AddAsync(TAggregate, CancellationToken); Task SaveAsync(TAggregate, CancellationToken); Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid, CancellationToken); }`

- [ ] **Step 1: Write the failing tests**

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.EventSourcing;

public class EventSourcedAggregateTests
{
    private sealed record CounterStarted(Guid CounterId) : IDomainEvent;
    private sealed record CounterIncremented(Guid CounterId, int By) : IDomainEvent;

    private sealed class Counter : EventSourcedAggregate
    {
        public int Value { get; private set; }

        public static Counter Start(Guid id)
        {
            var counter = new Counter();
            counter.Raise(new CounterStarted(id));
            return counter;
        }

        public void Increment(int by) => Raise(new CounterIncremented(Id, by));

        public void Retire() => MarkForArchive();

        public void Apply(CounterStarted e) => Id = e.CounterId;

        public void Apply(CounterIncremented e) => Value += e.By;

        protected override void When(IDomainEvent @event)
        {
            switch (@event)
            {
                case CounterStarted e: Apply(e); break;
                case CounterIncremented e: Apply(e); break;
                default: throw new InvalidOperationException($"Unhandled event {@event.GetType().Name}");
            }
        }
    }

    [Fact]
    public void Raise_applies_records_and_registers_the_event()
    {
        var id = Guid.NewGuid();
        var counter = Counter.Start(id);
        counter.Increment(3);

        Assert.Equal(id, counter.Id);
        Assert.Equal(3, counter.Value);
        Assert.Equal(2, counter.UncommittedEvents.Count);
        Assert.Equal(2, counter.DomainEvents.Count); // same events are the in-process domain events
        Assert.IsType<CounterIncremented>(counter.UncommittedEvents[1]);
    }

    [Fact]
    public void Version_is_zero_for_a_new_aggregate_and_settable_by_the_store()
    {
        var counter = Counter.Start(Guid.NewGuid());
        Assert.Equal(0, counter.Version);

        ((IEventSourcedAggregate)counter).SetVersion(7);
        Assert.Equal(7, counter.Version);
    }

    [Fact]
    public void ClearUncommittedEvents_leaves_domain_events_for_the_dispatcher()
    {
        var counter = Counter.Start(Guid.NewGuid());

        ((IEventSourcedAggregate)counter).ClearUncommittedEvents();

        Assert.Empty(counter.UncommittedEvents);
        Assert.Single(counter.DomainEvents);
    }

    [Fact]
    public void MarkForArchive_flags_the_aggregate()
    {
        var counter = Counter.Start(Guid.NewGuid());
        Assert.False(counter.IsMarkedForArchive);

        counter.Retire();

        Assert.True(counter.IsMarkedForArchive);
    }

    [Fact]
    public void Raise_rejects_null()
    {
        var counter = Counter.Start(Guid.NewGuid());
        Assert.Throws<ArgumentNullException>(() => counter.GetType()
            .GetMethod("Raise", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(counter, [null]) );
    }
}
```

Note: the last test's `Invoke` wraps the exception in `TargetInvocationException`; assert on `Assert.Throws<System.Reflection.TargetInvocationException>` and check `InnerException` is `ArgumentNullException` instead:

```csharp
    [Fact]
    public void Raise_rejects_null()
    {
        var counter = Counter.Start(Guid.NewGuid());
        var raise = typeof(EventSourcedAggregate).GetMethod("Raise",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => raise.Invoke(counter, [null]));

        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests --filter "FullyQualifiedName~EventSourcedAggregateTests"`
Expected: compile error — namespace `SharedKernel.EventSourcing` missing.

- [ ] **Step 3: Create `ITransactionParticipant`**

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

/// <summary>
/// A scoped unit-of-work participant that joins the transaction of a module <see cref="DbContext"/> — the
/// seam through which an event store (Marten, see <c>Shared/*.EventSourcing</c>) commits atomically with EF
/// writes and the co-located Wolverine outbox. <c>TransactionBehavior</c> treats a participant's
/// <see cref="Owner"/> as the module it mutates (one module per command still applies), flushes it inside the
/// owner's transaction on every drain-loop iteration, collects its domain events, and discards it on failure.
/// </summary>
public interface ITransactionParticipant
{
    /// <summary>The module DbContext whose transaction this participant enlists in.</summary>
    DbContext Owner { get; }

    /// <summary>True when the participant has staged work (appends, archives) that has not been flushed.</summary>
    bool HasPendingChanges { get; }

    /// <summary>
    /// Persists staged work inside <see cref="Owner"/>'s current transaction. Must never commit — the behavior
    /// owns commit/rollback. Called only while <c>Owner.Database.CurrentTransaction</c> is non-null.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken);

    /// <summary>Drains domain events from aggregates loaded or added through this participant.</summary>
    IEnumerable<IDomainEvent> TakeDomainEvents();

    /// <summary>Drops staged work and tracked aggregates (failure Result, exception, or rollback).</summary>
    void Discard();
}
```

- [ ] **Step 4: Create `IEventSourcedAggregate` and `EventSourcedAggregate`**

`IEventSourcedAggregate.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// Store-facing surface of an event-sourced aggregate. Repositories and the transaction participant use it
/// to read uncommitted events, stamp the stream version after a load, and clear events after a flush.
/// Application code should depend on the concrete aggregate, not on this interface.
/// </summary>
public interface IEventSourcedAggregate : IHasDomainEvents
{
    Guid Id { get; }

    /// <summary>Stream version at load time (0 for a new aggregate). Set by the repository, not by the aggregate.</summary>
    long Version { get; }

    /// <summary>Events raised since the last flush, in order. These are what the store appends.</summary>
    IReadOnlyList<IDomainEvent> UncommittedEvents { get; }

    /// <summary>True when the aggregate asked for its stream to be archived at the next save.</summary>
    bool IsMarkedForArchive { get; }

    void SetVersion(long version);

    void ClearUncommittedEvents();
}
```

`EventSourcedAggregate.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// Base type for an aggregate whose state is derived from an event stream instead of a table row
/// (ADR-0009). Opt-in per aggregate — most aggregates should stay on <see cref="Entity"/> + EF Core.
/// <para>
/// Shape: command methods validate invariants and call <see cref="Raise"/>; <see cref="Raise"/> applies the
/// event through <see cref="When"/>, records it as uncommitted (for the store) and registers it as a domain
/// event (for same-module <c>IDomainEventHandler&lt;T&gt;</c>s). State transitions live in
/// <c>public void Apply(TEvent)</c> methods that contain no validation — the event store replays history
/// through those same methods by naming convention, so this class never references the store.
/// </para>
/// <para>
/// <b>Do not name the dispatcher <c>Apply</c>.</b> The store's convention scanner also binds interface-typed
/// <c>Apply</c> overloads; an <c>Apply(IDomainEvent)</c> would make it apply every event twice.
/// </para>
/// </summary>
public abstract class EventSourcedAggregate : Entity<Guid>, IAggregateRoot, IEventSourcedAggregate
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    public long Version { get; private set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public bool IsMarkedForArchive { get; private set; }

    /// <summary>Applies the event to in-memory state, then records it for persistence and dispatch.</summary>
    protected void Raise(IDomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        When(@event);
        _uncommittedEvents.Add(@event);
        RegisterDomainEvent(@event);
    }

    /// <summary>Routes an event to its <c>Apply(TEvent)</c> method. Pure state transition; no validation.</summary>
    protected abstract void When(IDomainEvent @event);

    /// <summary>
    /// Requests that the stream be archived when the aggregate is saved (e.g. on a terminal "closed" event).
    /// Archived streams are excluded from default queries and projections but never deleted.
    /// </summary>
    protected void MarkForArchive() => IsMarkedForArchive = true;

    void IEventSourcedAggregate.SetVersion(long version) => Version = version;

    void IEventSourcedAggregate.ClearUncommittedEvents() => _uncommittedEvents.Clear();
}
```

- [ ] **Step 5: Create `StoredEvent` and `IEventSourcedRepository`**

`StoredEvent.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// One persisted event with its store metadata — what an audit/history read returns. <see cref="Version"/> is
/// the position within the stream; <see cref="Sequence"/> is the store-wide sequence number.
/// </summary>
public sealed record StoredEvent(
    long Version,
    long Sequence,
    DateTimeOffset Timestamp,
    string EventType,
    string? CorrelationId,
    IReadOnlyDictionary<string, object?> Headers,
    IDomainEvent Data);
```

`IEventSourcedRepository.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// Persistence contract for an event-sourced aggregate. Bespoke repositories extend it
/// (<c>IAccountRepository : IEventSourcedRepository&lt;Account&gt;</c>) — golden rule 4 applies unchanged.
/// <para>
/// <see cref="LoadAsync"/> declares write intent: it opens the module transaction if needed and tracks the
/// stream's version for optimistic concurrency. Use a projection query (not the stream) for reads.
/// </para>
/// </summary>
public interface IEventSourcedRepository<TAggregate>
    where TAggregate : EventSourcedAggregate
{
    /// <summary>Loads the current state for writing, or <c>null</c> when the stream does not exist.</summary>
    Task<TAggregate?> LoadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Starts a new stream from the aggregate's uncommitted events.</summary>
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends the aggregate's uncommitted events to a stream previously obtained via <see cref="LoadAsync"/>,
    /// with the loaded version as the expected version; archives the stream if requested.
    /// </summary>
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>Returns the full stream with metadata (audit trail). Read-only; no transaction.</summary>
    Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests --filter "FullyQualifiedName~EventSourcedAggregateTests"`
Expected: 5 passed. `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.SharedKernel/Persistence/ITransactionParticipant.cs Shared/AllSpice.CleanModularMonolith.SharedKernel/EventSourcing tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/EventSourcing
git commit -m "feat(sharedkernel): event-sourced aggregate base, repository contract, transaction participant seam"
```

### Task 4: Participant-aware `TransactionBehavior`

**Files:**
- Modify: `Shared/AllSpice.CleanModularMonolith.SharedKernel/Behaviors/TransactionBehavior.cs` (full rewrite of `Handle`)
- Modify: `tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Behaviors/TransactionBehaviorFailureResultTests.cs` (`CreateBehavior`)
- Modify: `tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Behaviors/TransactionBehaviorOutboxFlushTests.cs` (`CreateBehavior`)
- Test: `tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Behaviors/TransactionBehaviorParticipantTests.cs`

**Interfaces:**
- Consumes: `ITransactionParticipant`, `ConcurrencyConflictException` (Tasks 2–3).
- Produces: new constructor `TransactionBehavior(IEnumerable<IModuleDbContext> dbContexts, IEnumerable<ITransactionParticipant> participants, IDomainEventDispatcher dispatcher, IEnumerable<IOutboxFlusher> outboxFlushers, IPostCommitActions postCommitActions, ILogger<…> logger)`. Behaviour rules 1–4 from spec §4.3.

- [ ] **Step 1: Find every construction site of the behavior**

Run: `grep -rn "new TransactionBehavior\|TransactionBehavior<" --include=*.cs . | grep -v "/obj/"`
Expected: the two existing unit-test files (`CreateBehavior`) plus the DI registration (open generic via `AddMediator`/pipeline options — no explicit `new`). Any other explicit `new` you find must also receive the `participants` argument (`[]`).

- [ ] **Step 2: Write the failing participant tests**

Create `TransactionBehaviorParticipantTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Behaviors;

/// <summary>
/// An event store joins the module transaction through <see cref="ITransactionParticipant"/>. These tests pin
/// the four contract points: a participant with pending work counts as a dirty module (a transaction is opened
/// and committed even with no EF changes); a transaction the participant opened EARLY is reused, not
/// re-opened; the participant is flushed inside the transaction and its domain events dispatched; and both
/// failure paths (failure Result / exception) discard the participant and roll the early transaction back.
/// Real SQLite so transactions are real.
/// </summary>
public sealed class TransactionBehaviorParticipantTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestModuleDbContext _db;
    private readonly TestModuleDbContext _otherDb;
    private readonly SqliteConnection _otherConnection;

    public TransactionBehaviorParticipantTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TestModuleDbContext(Options(_connection));
        _db.Database.EnsureCreated();
        _db.Database.ExecuteSqlRaw("CREATE TABLE Flushes (Note TEXT NOT NULL)");

        _otherConnection = new SqliteConnection("DataSource=:memory:");
        _otherConnection.Open();
        _otherDb = new TestModuleDbContext(Options(_otherConnection));
        _otherDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _otherDb.Dispose();
        _otherConnection.Dispose();
    }

    [Fact]
    public async Task Participant_with_pending_work_and_no_EF_changes_is_flushed_and_committed()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        var result = await behavior.Handle(new FakeCommand(), (_, _) =>
        {
            participant.Stage("appended"); // no EF entity staged — only the participant is dirty
            return ValueTask.FromResult(Result.Success());
        }, CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(1, participant.FlushCount);
        Assert.Equal(1, await CountFlushesAsync("appended"));
        Assert.Null(_db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Early_opened_transaction_is_reused_not_reopened()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await participant.OpenEarlyAsync(ct); // simulates FetchForWriting needing a session mid-handler
            participant.Stage("early");
            _db.Items.Add(new TestEntity { Name = "row" });
            return Result.Success();
        }, CancellationToken.None);

        Assert.Same(participant.EarlyTransaction, participant.TransactionSeenAtFlush);
        Assert.Equal(1, await CountFlushesAsync("early"));
        await using var fresh = new TestModuleDbContext(Options(_connection));
        Assert.True(await fresh.Items.AnyAsync(i => i.Name == "row"));
        Assert.Null(_db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Participant_domain_events_are_dispatched_and_second_generation_work_is_flushed()
    {
        var participant = new FakeParticipant(_db);
        var dispatched = new List<IDomainEvent>();
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<IDomainEvent> events, CancellationToken _) =>
            {
                dispatched.AddRange(events);
                // a handler reacting to the first event appends more work to the SAME participant
                if (dispatched.Count == 1) participant.Stage("second-generation");
                return Task.CompletedTask;
            });
        var behavior = CreateBehavior(participant, dispatcher.Object);

        await behavior.Handle(new FakeCommand(), (_, _) =>
        {
            participant.Stage("first");
            participant.RaiseDomainEvent(new FakeDomainEvent());
            return ValueTask.FromResult(Result.Success());
        }, CancellationToken.None);

        Assert.Single(dispatched);
        Assert.Equal(2, participant.FlushCount);
        Assert.Equal(1, await CountFlushesAsync("second-generation"));
    }

    [Fact]
    public async Task Failure_result_discards_participant_and_rolls_back_early_transaction()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        var result = await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await participant.OpenEarlyAsync(ct);
            participant.Stage("never");
            return Result.Conflict("busy");
        }, CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.True(participant.Discarded);
        Assert.Equal(0, participant.FlushCount);
        Assert.Null(_db.Database.CurrentTransaction);
        Assert.Equal(0, await CountFlushesAsync("never"));
    }

    [Fact]
    public async Task Exception_after_flush_rolls_everything_back_and_discards()
    {
        var participant = new FakeParticipant(_db) { ThrowOnFlush = true };
        var behavior = CreateBehavior(participant);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FakeCommand(), (_, _) =>
            {
                _db.Items.Add(new TestEntity { Name = "doomed" });
                participant.Stage("doomed");
                return ValueTask.FromResult(Result.Success());
            }, CancellationToken.None));

        Assert.True(participant.Discarded);
        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Null(_db.Database.CurrentTransaction);
        await using var fresh = new TestModuleDbContext(Options(_connection));
        Assert.False(await fresh.Items.AnyAsync(i => i.Name == "doomed"));
    }

    [Fact]
    public async Task Participant_owner_counts_toward_the_one_module_rule()
    {
        var participant = new FakeParticipant(_otherDb); // participant belongs to ANOTHER module
        var behavior = CreateBehavior(participant, dbContexts: [_db, _otherDb]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FakeCommand(), (_, _) =>
            {
                _db.Items.Add(new TestEntity { Name = "mine" });
                participant.Stage("theirs");
                return ValueTask.FromResult(Result.Success());
            }, CancellationToken.None));

        Assert.Contains("multiple module DbContexts", ex.Message);
        Assert.True(participant.Discarded);
    }

    [Fact]
    public async Task Early_transaction_with_nothing_pending_is_released()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await participant.OpenEarlyAsync(ct); // loaded an aggregate, raised nothing
            return Result.Success();
        }, CancellationToken.None);

        Assert.Null(_db.Database.CurrentTransaction);
        Assert.Equal(0, participant.FlushCount);
    }

    private async Task<int> CountFlushesAsync(string note)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Flushes WHERE Note = $note";
        cmd.Parameters.AddWithValue("$note", note);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private TransactionBehavior<FakeCommand, Result> CreateBehavior(
        FakeParticipant participant,
        IDomainEventDispatcher? dispatcher = null,
        IEnumerable<IModuleDbContext>? dbContexts = null) =>
        new(dbContexts ?? [_db], [participant], dispatcher ?? Mock.Of<IDomainEventDispatcher>(), [],
            new PostCommitActions(), NullLogger<TransactionBehavior<FakeCommand, Result>>.Instance);

    private static DbContextOptions<TestModuleDbContext> Options(SqliteConnection connection) =>
        new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(connection).Options;

    private sealed record FakeCommand : IMessage, ITransactional;

    private sealed record FakeDomainEvent : IDomainEvent;

    /// <summary>
    /// Stands in for the Marten participant: "flushing" writes a row through the owner's CURRENT transaction
    /// (raw SQL on the same connection), so a rollback must make the row vanish and a commit must keep it.
    /// </summary>
    private sealed class FakeParticipant(TestModuleDbContext owner) : ITransactionParticipant
    {
        private readonly List<string> _staged = [];
        private readonly List<IDomainEvent> _events = [];

        public DbContext Owner => owner;
        public bool HasPendingChanges => _staged.Count > 0;
        public int FlushCount { get; private set; }
        public bool Discarded { get; private set; }
        public bool ThrowOnFlush { get; init; }
        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? EarlyTransaction { get; private set; }
        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? TransactionSeenAtFlush { get; private set; }

        public async Task OpenEarlyAsync(CancellationToken ct) =>
            EarlyTransaction = owner.Database.CurrentTransaction ?? await owner.Database.BeginTransactionAsync(ct);

        public void Stage(string note) => _staged.Add(note);

        public void RaiseDomainEvent(IDomainEvent e) => _events.Add(e);

        public async ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            TransactionSeenAtFlush = owner.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Flush called without a transaction");
            if (ThrowOnFlush) throw new InvalidOperationException("flush failed");
            foreach (var note in _staged)
            {
                await owner.Database.ExecuteSqlRawAsync("INSERT INTO Flushes (Note) VALUES ({0})", [note], cancellationToken);
            }
            _staged.Clear();
            FlushCount++;
        }

        public IEnumerable<IDomainEvent> TakeDomainEvents()
        {
            var events = _events.ToArray();
            _events.Clear();
            return events;
        }

        public void Discard()
        {
            Discarded = true;
            _staged.Clear();
            _events.Clear();
        }
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options)
        : DbContext(options), IModuleDbContext
    {
        public DbSet<TestEntity> Items => Set<TestEntity>();
        public DbContext Instance => this;
    }
}
```

- [ ] **Step 3: Update the two existing test files' `CreateBehavior`**

In `TransactionBehaviorFailureResultTests.cs` and `TransactionBehaviorOutboxFlushTests.cs` change the `new(...)` in `CreateBehavior` to pass an empty participant list as the **second** argument, e.g.:

```csharp
        new([_db], [], Mock.Of<IDomainEventDispatcher>(), [], new PostCommitActions(),
            NullLogger<TransactionBehavior<FakeCommand, Result>>.Instance);
```
(keep whatever the file already passes for the other arguments — only insert `[]` after the contexts).

- [ ] **Step 4: Run to verify failure**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests --filter "FullyQualifiedName~TransactionBehavior"`
Expected: compile error — constructor has no `participants` parameter.

- [ ] **Step 5: Rewrite `TransactionBehavior`**

Replace the whole file with:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Pipeline behavior that owns the unit-of-work boundary for <see cref="ITransactional"/> commands.
/// <para>
/// Repositories stage writes only (see <c>EfRepository.SaveChangesAsync</c>), so the handler performs no
/// database writes itself. After the handler returns, this behavior finds the single dirty module
/// <see cref="IModuleDbContext"/>, opens a transaction <b>on that context</b> (or reuses one an
/// <see cref="ITransactionParticipant"/> opened early), flushes the staged writes and the participants,
/// drains domain events (which may stage more and may publish integration events that enrol the same
/// transaction's outbox), then commits — or rolls everything back on any failure.
/// </para>
/// <para>
/// <b>Participants.</b> An event store (Marten) cannot stage-only: its write API needs a live session during
/// the handler, and that session must sit inside the module transaction to be atomic with the outbox. So the
/// module transaction is opened <i>lazily</i> — here at commit time, or earlier by a participant on first
/// write-intent. Either way this behavior is the only thing that commits or rolls back. A participant's
/// <see cref="ITransactionParticipant.Owner"/> counts as the module it mutates (one module per command).
/// </para>
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IEnumerable<IModuleDbContext> dbContexts,
    IEnumerable<ITransactionParticipant> participants,
    IDomainEventDispatcher dispatcher,
    IEnumerable<IOutboxFlusher> outboxFlushers,
    IPostCommitActions postCommitActions,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage, ITransactional
{
    private readonly IEnumerable<IModuleDbContext> _dbContexts = dbContexts;
    private readonly IReadOnlyList<ITransactionParticipant> _participants = participants.ToList();
    private readonly IDomainEventDispatcher _dispatcher = dispatcher;
    private readonly IEnumerable<IOutboxFlusher> _outboxFlushers = outboxFlushers;
    private readonly IPostCommitActions _postCommitActions = postCommitActions;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response;
        try
        {
            // Repositories stage only, so the handler performs no DB writes itself — EXCEPT that an event-store
            // participant may have opened the module transaction early. If the handler throws, release that
            // transaction (nothing was flushed) and drop the participant's staged work.
            response = await next(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            throw;
        }

        // Exactly one module may be dirty: a context with tracked changes, or the owner of a participant with
        // pending work. Cross-module side effects must go through integration events (Wolverine outbox).
        var dirtyContexts = _dbContexts
            .Select(c => c.Instance)
            .Where(db => db.ChangeTracker.HasChanges() || _participants.Any(p => p.HasPendingChanges && ReferenceEquals(p.Owner, db)))
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<DbContext>()
            .ToList();

        if (dirtyContexts.Count == 0)
        {
            // Nothing to commit. A participant may still hold an early transaction (it loaded but raised nothing)
            // — release it so the connection isn't left inside an open transaction for the rest of the scope.
            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            return response;
        }

        // Failure must not mutate state. If the handler staged writes but signalled failure by RETURNING a
        // failure Result (instead of throwing), discard the staged changes rather than committing them.
        // Success is ResultStatus.Ok here (Created/NoContent are applied at the endpoint layer).
        if (response is IResult result &&
            result.Status is not (ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent))
        {
            _logger.LogWarning(
                "{RequestType} returned a failure Result ({Status}) after staging writes to {DbContext}; " +
                "discarding the staged changes without committing.",
                typeof(TRequest).Name,
                result.Status,
                dirtyContexts[0].GetType().Name);

            // Clearing the change tracker is not optional: the module DbContext is scoped to the request, so
            // staged entities stay tracked after we return and a SUBSEQUENT ITransactional command in the same
            // scope would commit them. Participants are discarded and any early transaction rolled back.
            foreach (var dirty in dirtyContexts)
            {
                dirty.ChangeTracker.Clear();
            }

            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            return response;
        }

        if (dirtyContexts.Count > 1)
        {
            var contextNames = string.Join(", ", dirtyContexts.Select(c => c.GetType().Name));
            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} mutated multiple module DbContexts ({contextNames}). " +
                "A command must touch only one module. Cross-module side effects must be " +
                "published as integration events through IIntegrationEventPublisher so the " +
                "Wolverine outbox can deliver them transactionally.");
        }

        var db = dirtyContexts[0];
        var moduleParticipants = _participants.Where(p => ReferenceEquals(p.Owner, db)).ToList();

        // Reuse a transaction a participant opened early; otherwise open one now.
        var transaction = db.Database.CurrentTransaction
            ?? await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Using transaction {TransactionId} for {RequestType}",
            transaction.TransactionId, typeof(TRequest).Name);
        try
        {
            // Flush the handler's staged writes — EF first, then the participants — inside the transaction.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await FlushParticipantsAsync(moduleParticipants, cancellationToken).ConfigureAwait(false);

            // Drain-loop: dispatch domain events (including second-generation events raised by event
            // handlers) until none remain. Integration events are published here — by domain-event
            // handlers running inside this open transaction — so the publisher's "active transaction
            // required" guard is satisfied and the outbox envelope enrols this same transaction.
            bool hasMore = true;
            while (hasMore)
            {
                var events = db.ChangeTracker
                    .Entries<IHasDomainEvents>()
                    .SelectMany(e => e.Entity.TakeDomainEvents())
                    .Concat(moduleParticipants.SelectMany(p => p.TakeDomainEvents()))
                    .ToList();

                if (events.Count == 0)
                {
                    hasMore = false;
                    continue;
                }

                _logger.LogDebug("Dispatching {Count} domain events", events.Count);
                await _dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);

                // A domain-event handler must not write to a DIFFERENT module — via its context or a
                // participant. Re-check here because the pre-loop guard only saw the handler's own writes.
                var foreignDirty = _dbContexts.Select(c => c.Instance).FirstOrDefault(other =>
                    !ReferenceEquals(other, db) &&
                    (other.ChangeTracker.HasChanges() ||
                     _participants.Any(p => p.HasPendingChanges && ReferenceEquals(p.Owner, other))));
                if (foreignDirty is not null)
                {
                    throw new InvalidOperationException(
                        $"A domain-event handler for {typeof(TRequest).Name} mutated a different module " +
                        $"DbContext ({foreignDirty.GetType().Name}). Cross-module side effects must " +
                        "be published as integration events through IIntegrationEventPublisher.");
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await FlushParticipantsAsync(moduleParticipants, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Committed transaction {TransactionId} for {RequestType}",
                transaction.TransactionId, typeof(TRequest).Name);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            // RollbackAsync reverts the database but leaves the entities tracked in their staged state on the
            // scoped context. Clear them (and the participants) so they can't be re-flushed by a later
            // ITransactional command sharing this scope.
            db.ChangeTracker.Clear();
            foreach (var participant in _participants)
            {
                participant.Discard();
            }

            _logger.LogWarning("Rolled back transaction {TransactionId} for {RequestType}",
                transaction.TransactionId, typeof(TRequest).Name);

            // EF's optimistic-concurrency failure and the event store's version conflict are the same thing to a
            // client: reload and retry. Surface both as the one domain exception (409 concurrency_conflict).
            if (ex is DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException(
                    $"{typeof(TRequest).Name} lost a concurrency race: the data was modified by another request. Reload and retry.",
                    ex);
            }

            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        // The command's data AND the integration-event envelopes it enrolled are now committed atomically.
        // Release the envelopes so they are sent immediately instead of on the messaging layer's next durable
        // recovery sweep. Reached only on a successful commit — the catch above rethrows.
        await FlushOutboxAsync(cancellationToken).ConfigureAwait(false);

        // Run post-commit side effects the handler deferred (e.g. authz cache-eviction nudges). These MUST
        // run after commit: firing them from inside the handler would evict/publish before the write is
        // durable, so a concurrent read could re-cache stale data.
        await RunPostCommitActionsAsync(cancellationToken).ConfigureAwait(false);

        return response;
    }

    private static async ValueTask FlushParticipantsAsync(
        IReadOnlyList<ITransactionParticipant> moduleParticipants,
        CancellationToken cancellationToken)
    {
        foreach (var participant in moduleParticipants)
        {
            if (participant.HasPendingChanges)
            {
                await participant.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Rolls back and disposes any module transaction that is still open when there is nothing to commit
    /// (a participant opened it early and the command then failed, or staged nothing). Best-effort per
    /// context; the exception that caused the failure — if any — still propagates from the caller.
    /// </summary>
    private async ValueTask ReleaseOpenTransactionsAsync(bool discardParticipants)
    {
        if (discardParticipants)
        {
            foreach (var participant in _participants)
            {
                participant.Discard();
            }
        }

        foreach (var context in _dbContexts)
        {
            IDbContextTransaction? open = context.Instance.Database.CurrentTransaction;
            if (open is null)
            {
                continue;
            }

            try
            {
                await open.RollbackAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rolling back an early-opened transaction for {RequestType} failed.", typeof(TRequest).Name);
            }
            finally
            {
                await open.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Runs actions queued via <see cref="IPostCommitActions"/> after a successful commit. Best-effort: a
    /// failure is logged and swallowed because the command is already committed — failing it now would be
    /// wrong, and cache-eviction actions are self-healing via their TTL backstop.
    /// </summary>
    private async ValueTask RunPostCommitActionsAsync(CancellationToken cancellationToken)
    {
        foreach (var action in _postCommitActions.Drain())
        {
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Post-commit action failed for {RequestType}; the command is already committed.",
                    typeof(TRequest).Name);
            }
        }
    }

    /// <summary>
    /// Invokes every registered <see cref="IOutboxFlusher"/> after commit. Best-effort: a flush failure is
    /// swallowed (logged) because the envelope is already durably persisted — the recovery loop will still
    /// deliver it, and failing an already-committed command would be wrong. No-op when none is registered.
    /// </summary>
    private async ValueTask FlushOutboxAsync(CancellationToken cancellationToken)
    {
        foreach (var flusher in _outboxFlushers)
        {
            try
            {
                await flusher.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Outbox flush after commit failed for {RequestType}; the persisted envelope(s) will be " +
                    "delivered by the durable recovery loop instead.", typeof(TRequest).Name);
            }
        }
    }
}
```

Notes for the implementer:
- `Distinct(ReferenceEqualityComparer.Instance)` needs `.Cast<DbContext>()` because the comparer is `IEqualityComparer<object>`; the code above does that. If the compiler complains, replace with a manual loop that adds to a `List<DbContext>` when `!list.Any(x => ReferenceEquals(x, db))`.
- The `IResult` here is `Ardalis.Result.IResult` (already the case in the current file).

- [ ] **Step 6: Run all behavior tests**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests --filter "FullyQualifiedName~TransactionBehavior"`
Expected: all pass (7 new + existing). Then the full solution: `dotnet build AllSpice.CleanModularMonolith.slnx && dotnet test AllSpice.CleanModularMonolith.slnx --no-build` → green (Foundation tests construct no behavior directly, but they exercise the Mediator pipeline; DI resolves `IEnumerable<ITransactionParticipant>` as empty).

- [ ] **Step 7: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.SharedKernel/Behaviors/TransactionBehavior.cs tests/AllSpice.CleanModularMonolith.SharedKernel.UnitTests/Behaviors
git commit -m "feat(sharedkernel): TransactionBehavior enlists ITransactionParticipants (early tx reuse, flush per drain, discard on failure)"
```

---

### Task 5: `EventSourcing` shared project — participant, metadata, registration

**Files:**
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/AllSpice.CleanModularMonolith.EventSourcing.csproj`
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/IEventMetadataProvider.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/NullEventMetadataProvider.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/IModuleEventStoreSession.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/MartenTransactionParticipant.cs`
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/ModuleEventStoreExtensions.cs`
- Modify: `AllSpice.CleanModularMonolith.slnx` (add project under `/Shared/`)

**Interfaces:**
- Consumes: `ITransactionParticipant`, `IEventSourcedAggregate`, `ConcurrencyConflictException`, `ConflictException`.
- Produces:
  - `IEventMetadataProvider { string? CorrelationId; string? IdempotencyKey; }`
  - `IModuleEventStoreSession<TStore> { ValueTask<IDocumentSession> GetSessionAsync(CancellationToken); IQuerySession OpenQuerySession(); void Track(IEventSourcedAggregate); void MarkPending(); }`
  - `MartenTransactionParticipant<TStore, TContext>` (scoped) implementing both.
  - `IHostApplicationBuilder AddModuleEventStore<TStore, TContext>(this IHostApplicationBuilder, string connectionString, string schemaName, Action<StoreOptions>? configure = null)`
  - `Task ApplyEventStoreSchemaAsync<TStore>(this IServiceProvider, CancellationToken)`
  - Header key constant `EventHeaders.IdempotencyKey == "idempotency-key"`.

- [ ] **Step 1: Create the project**

`AllSpice.CleanModularMonolith.EventSourcing.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Marten" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Ardalis.GuardClauses" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AllSpice.CleanModularMonolith.SharedKernel\AllSpice.CleanModularMonolith.SharedKernel.csproj" />
  </ItemGroup>

</Project>
```

Add to the solution: `dotnet sln AllSpice.CleanModularMonolith.slnx add Shared/AllSpice.CleanModularMonolith.EventSourcing/AllSpice.CleanModularMonolith.EventSourcing.csproj --solution-folder Shared`
(If the CLI refuses `.slnx`, add the `<Project Path="Shared/AllSpice.CleanModularMonolith.EventSourcing/AllSpice.CleanModularMonolith.EventSourcing.csproj" />` line by hand inside `<Folder Name="/Shared/">`.)

- [ ] **Step 2: Metadata provider**

`IEventMetadataProvider.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Supplies per-request metadata stamped on every appended event: the correlation id (ties the event to the
/// HTTP request/log scope) and the client's <c>Idempotency-Key</c> (lets a replayed command be recognised at
/// the stream level). The gateway implements it from <c>HttpContext</c>; background flows fall back to
/// <see cref="NullEventMetadataProvider"/>.
/// </summary>
public interface IEventMetadataProvider
{
    string? CorrelationId { get; }

    string? IdempotencyKey { get; }
}

/// <summary>Header keys used in event metadata. Centralised so readers and writers cannot drift.</summary>
public static class EventHeaders
{
    public const string IdempotencyKey = "idempotency-key";
}
```

`NullEventMetadataProvider.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>Default when no request context exists (jobs, tests): stamps nothing.</summary>
public sealed class NullEventMetadataProvider : IEventMetadataProvider
{
    public string? CorrelationId => null;

    public string? IdempotencyKey => null;
}
```

- [ ] **Step 3: Session contract for repositories**

`IModuleEventStoreSession.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using Marten;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// What a module's event-sourced repositories need from the transaction participant: a write session that
/// is enlisted in the module transaction, a throw-away query session for reads, and a way to register the
/// aggregates whose events must be dispatched and cleared. Typed by the module's store marker so two modules'
/// stores can coexist in one scope.
/// </summary>
/// <typeparam name="TStore">The module's <see cref="IDocumentStore"/> marker interface.</typeparam>
public interface IModuleEventStoreSession<TStore>
    where TStore : IDocumentStore
{
    /// <summary>
    /// Returns the scope's write session, opening the module transaction and the session on first use.
    /// Declares write intent — never call it from a query handler; use <see cref="OpenQuerySession"/>.
    /// </summary>
    ValueTask<IDocumentSession> GetSessionAsync(CancellationToken cancellationToken);

    /// <summary>Opens a read-only session on its own connection (no transaction). Dispose it.</summary>
    IQuerySession OpenQuerySession();

    /// <summary>Registers an aggregate so its domain events are dispatched and its events cleared after flush.</summary>
    void Track(IEventSourcedAggregate aggregate);

    /// <summary>Signals staged work (append/start/archive) that the next flush must persist.</summary>
    void MarkPending();
}
```

- [ ] **Step 4: The participant**

`MartenTransactionParticipant.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Marten;
using Marten.Exceptions;
using Marten.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Scoped bridge between a module's Marten store and its EF Core <see cref="DbContext"/> transaction.
/// <para>
/// On first <see cref="GetSessionAsync"/> it ensures the owner context has a transaction (beginning one if
/// the handler got here before <c>TransactionBehavior</c>) and opens a Marten session enlisted in that same
/// Npgsql transaction with <c>shouldAutoCommit: false</c> — so <c>SaveChangesAsync</c> on the session writes
/// events and inline projections but never commits. Commit/rollback stay with <c>TransactionBehavior</c>,
/// which calls <see cref="FlushAsync"/> inside the transaction and <see cref="Discard"/> on failure.
/// </para>
/// </summary>
/// <typeparam name="TStore">The module's store marker (<c>ILedgerEventStore : IDocumentStore</c>).</typeparam>
/// <typeparam name="TContext">The module's DbContext (transaction owner, outbox host).</typeparam>
public sealed class MartenTransactionParticipant<TStore, TContext>(
    TStore store,
    TContext owner,
    IEventMetadataProvider metadata,
    ILogger<MartenTransactionParticipant<TStore, TContext>> logger)
    : ITransactionParticipant, IModuleEventStoreSession<TStore>, IAsyncDisposable
    where TStore : IDocumentStore
    where TContext : DbContext, IModuleDbContext
{
    private readonly TStore _store = store;
    private readonly TContext _owner = owner;
    private readonly IEventMetadataProvider _metadata = metadata;
    private readonly ILogger<MartenTransactionParticipant<TStore, TContext>> _logger = logger;
    private readonly List<IEventSourcedAggregate> _tracked = [];
    private IDocumentSession? _session;
    private bool _hasPendingChanges;

    public DbContext Owner => _owner;

    public bool HasPendingChanges => _hasPendingChanges;

    public async ValueTask<IDocumentSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return _session;
        }

        // Open the module transaction early if TransactionBehavior hasn't yet — it will detect and reuse it.
        IDbContextTransaction transaction = _owner.Database.CurrentTransaction
            ?? await _owner.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (transaction.GetDbTransaction() is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException(
                $"{typeof(TContext).Name} is not using Npgsql; the Marten event store can only enlist in a PostgreSQL transaction.");
        }

        _session = _store.LightweightSession(SessionOptions.ForTransaction(npgsqlTransaction, shouldAutoCommit: false));
        _session.CorrelationId = _metadata.CorrelationId;
        if (_metadata.IdempotencyKey is { Length: > 0 } idempotencyKey)
        {
            _session.SetHeader(EventHeaders.IdempotencyKey, idempotencyKey);
        }

        _logger.LogDebug("Opened {Store} session inside transaction {TransactionId}",
            typeof(TStore).Name, transaction.TransactionId);
        return _session;
    }

    public IQuerySession OpenQuerySession() => _store.QuerySession();

    public void Track(IEventSourcedAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (!_tracked.Any(a => ReferenceEquals(a, aggregate)))
        {
            _tracked.Add(aggregate);
        }
    }

    public void MarkPending() => _hasPendingChanges = true;

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        if (_session is null || !_hasPendingChanges)
        {
            return;
        }

        if (_owner.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Event store flush requested without an active module transaction. TransactionBehavior must own the transaction.");
        }

        try
        {
            await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyException ex)
        {
            // Another request appended to the same stream between our FetchForWriting and this flush.
            throw new ConcurrencyConflictException(
                "The aggregate was modified by another request after it was loaded. Reload and retry.", ex);
        }
        catch (ExistingStreamIdCollisionException ex)
        {
            throw new ConflictException("Event stream", ex.Id);
        }

        foreach (var aggregate in _tracked)
        {
            aggregate.ClearUncommittedEvents();
        }

        _hasPendingChanges = false;
    }

    public IEnumerable<IDomainEvent> TakeDomainEvents() =>
        _tracked.SelectMany(a => a.TakeDomainEvents()).ToList();

    public void Discard()
    {
        _tracked.Clear();
        _hasPendingChanges = false;
        _session?.Dispose();
        _session = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
        }
    }
}
```

Implementer notes: `ExistingStreamIdCollisionException.Id` is `object` — if the property name differs in Marten 9.33, use whatever exposes the stream id (fall back to `ex.Message` as the key). `SessionOptions.ForTransaction` takes a `DbTransaction`; the explicit `NpgsqlTransaction` check gives a clear error for SQLite-backed tests instead of a Marten cast failure.

- [ ] **Step 5: Registration extension**

`ModuleEventStoreExtensions.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Wires one module's Marten event store as an <b>ancillary</b> store living in that module's own database,
/// enlisted in the module DbContext transaction via <see cref="MartenTransactionParticipant{TStore,TContext}"/>.
/// A module with no event-sourced aggregates never calls this and never loads Marten.
/// </summary>
public static class ModuleEventStoreExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TStore"/> with the template's fixed settings and the transaction
    /// participant. <paramref name="configure"/> adds the module's projections, event-type mappings and
    /// upcasters.
    /// </summary>
    /// <param name="connectionString">The module database — the SAME database as <typeparamref name="TContext"/>.</param>
    /// <param name="schemaName">Postgres schema for the event tables (the module name, e.g. <c>ledger</c>); the
    /// outbox stays in <c>wolverine</c> and EF tables in <c>public</c>.</param>
    public static IHostApplicationBuilder AddModuleEventStore<TStore, TContext>(
        this IHostApplicationBuilder builder,
        string connectionString,
        string schemaName,
        Action<StoreOptions>? configure = null)
        where TStore : class, IDocumentStore
        where TContext : DbContext, IModuleDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        builder.Services.AddMartenStore<TStore>(opts =>
        {
            opts.Connection(connectionString);
            opts.DatabaseSchemaName = schemaName;

            // Schema is applied explicitly at startup (ApplyEventStoreSchemaAsync), like EF migrations — never
            // lazily on first use, and never dropping anything.
            opts.AutoCreateSchemaObjects = AutoCreate.None;

            opts.UseSystemTextJsonForSerialization();

            // Marten 9 defaults to QuickWithServerTimestamps, which withholds stream versions from inline
            // projections and forbids expected-version appends. Rich mode gives deterministic versions, which
            // the projections (Version column) and optimistic concurrency rely on.
            opts.Events.AppendMode = EventAppendMode.Rich;
            opts.Events.StreamIdentity = StreamIdentity.AsGuid;

            // Correlation id + headers (Idempotency-Key) on every event — see MartenTransactionParticipant.
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;

            configure?.Invoke(opts);
        });

        // A host that has no request context (tests, jobs) gets the null provider; the gateway registers its
        // HttpContext-backed provider BEFORE modules so TryAdd leaves it in place.
        builder.Services.TryAddScoped<IEventMetadataProvider, NullEventMetadataProvider>();

        builder.Services.AddScoped<MartenTransactionParticipant<TStore, TContext>>();
        builder.Services.AddScoped<ITransactionParticipant>(sp =>
            sp.GetRequiredService<MartenTransactionParticipant<TStore, TContext>>());
        builder.Services.AddScoped<IModuleEventStoreSession<TStore>>(sp =>
            sp.GetRequiredService<MartenTransactionParticipant<TStore, TContext>>());

        return builder;
    }

    /// <summary>
    /// Creates or updates the store's tables/functions in its schema. Call from
    /// <c>Ensure{Module}ModuleDatabaseAsync</c> after the EF migration; Marten serialises concurrent callers
    /// with its own advisory lock, so multiple replicas booting together are safe.
    /// </summary>
    public static async Task ApplyEventStoreSchemaAsync<TStore>(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TStore : IDocumentStore
    {
        var store = services.GetRequiredService<TStore>();
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync(ct: cancellationToken).ConfigureAwait(false);
    }
}
```

Implementer notes: `AutoCreate` lives in the `JasperFx` namespace in Marten 9 (older docs say `Weasel.Core`) — follow the compiler. `EventAppendMode` / `StreamIdentity` are in `JasperFx.Events` or `Marten.Events`; adjust the `using`s. The `ApplyAllConfiguredChangesToDatabaseAsync` overload takes `(AutoCreate? override = null, ReconnectionOptions? = null, CancellationToken ct = default)` in recent Weasel — pass the token by name as shown, or positionally if names differ.

- [ ] **Step 6: Build**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx`
Expected: 0 warnings, 0 errors. Fix `using`/API-name drift against Marten 9.33 here — this is the point where names are pinned to reality; do not change the public shapes listed under *Interfaces*.

- [ ] **Step 7: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.EventSourcing AllSpice.CleanModularMonolith.slnx
git commit -m "feat(eventsourcing): Marten transaction participant and per-module store registration"
```

---

### Task 6: `MartenEventSourcedRepository` + probe integration tests (proves the enlistment)

**Files:**
- Create: `Shared/AllSpice.CleanModularMonolith.EventSourcing/MartenEventSourcedRepository.cs`
- Create: `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.csproj`
- Create: `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/Probe/ProbeCounter.cs`
- Create: `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/Probe/ProbeDbContext.cs`
- Create: `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/Probe/ProbeCounterRepository.cs`
- Create: `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/ProbeHostFixture.cs`
- Create: `tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/MartenEnlistmentTests.cs`
- Modify: `AllSpice.CleanModularMonolith.slnx` (add under `/Tests/`)

**Interfaces:**
- Consumes: Task 5 types.
- Produces: `abstract class MartenEventSourcedRepository<TAggregate, TStore>(IModuleEventStoreSession<TStore> session) : IEventSourcedRepository<TAggregate>` with `protected IModuleEventStoreSession<TStore> Session`.

- [ ] **Step 1: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Marten" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.EventSourcing\AllSpice.CleanModularMonolith.EventSourcing.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.SharedKernel\AllSpice.CleanModularMonolith.SharedKernel.csproj" />
  </ItemGroup>

</Project>
```

Add to the solution: `dotnet sln AllSpice.CleanModularMonolith.slnx add tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.csproj --solution-folder Tests`

- [ ] **Step 2: Probe aggregate, context and repository**

`Probe/ProbeCounter.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;

public sealed record CounterStarted(Guid CounterId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record CounterIncremented(Guid CounterId, int By, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record CounterRetired(Guid CounterId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

/// <summary>Minimal event-sourced aggregate used to prove the store mechanics independent of any module.</summary>
public sealed class ProbeCounter : EventSourcedAggregate
{
    private ProbeCounter()
    {
    }

    public int Value { get; private set; }
    public bool Retired { get; private set; }

    public static ProbeCounter Start(Guid id, DateTimeOffset nowUtc)
    {
        var counter = new ProbeCounter();
        counter.Raise(new CounterStarted(id, nowUtc));
        return counter;
    }

    public void Increment(int by, DateTimeOffset nowUtc) => Raise(new CounterIncremented(Id, by, nowUtc));

    public void Retire(DateTimeOffset nowUtc)
    {
        Raise(new CounterRetired(Id, nowUtc));
        MarkForArchive();
    }

    public void Apply(CounterStarted e) => Id = e.CounterId;
    public void Apply(CounterIncremented e) => Value += e.By;
    public void Apply(CounterRetired e) => Retired = true;

    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case CounterStarted e: Apply(e); break;
            case CounterIncremented e: Apply(e); break;
            case CounterRetired e: Apply(e); break;
            default: throw new InvalidOperationException($"Unhandled event {@event.GetType().Name}");
        }
    }
}
```

`Probe/ProbeDbContext.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;

public sealed class ProbeRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Stands in for a module DbContext: owns the transaction and has one ordinary EF table.</summary>
public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options), IModuleDbContext
{
    public DbSet<ProbeRow> Rows => Set<ProbeRow>();

    public DbContext Instance => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ProbeRow>().HasKey(r => r.Id);
}

public interface IProbeEventStore : Marten.IDocumentStore
{
}
```

`Probe/ProbeCounterRepository.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;

public sealed class ProbeCounterRepository(IModuleEventStoreSession<IProbeEventStore> session)
    : MartenEventSourcedRepository<ProbeCounter, IProbeEventStore>(session);
```

- [ ] **Step 3: Host fixture**

`ProbeHostFixture.cs`:

```csharp
using AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests;

/// <summary>
/// One Postgres container + one host per test class. The host wires the probe DbContext and the probe
/// Marten store exactly the way a module would (AddModuleEventStore) — EF tables via EnsureCreated, Marten
/// schema via ApplyEventStoreSchemaAsync.
/// </summary>
public sealed class ProbeHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public IHost Host { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<ProbeDbContext>(o => o.UseNpgsql(ConnectionString));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<ProbeDbContext>());
        builder.AddModuleEventStore<IProbeEventStore, ProbeDbContext>(ConnectionString, "probe");
        builder.Services.AddScoped<ProbeCounterRepository>();

        Host = builder.Build();

        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.EnsureCreatedAsync();
        }

        await Host.Services.ApplyEventStoreSchemaAsync<IProbeEventStore>();
        await Host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await _postgres.DisposeAsync();
    }
}
```

- [ ] **Step 4: Write the failing tests**

`MartenEnlistmentTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests;

/// <summary>
/// Proves the enlistment contract on real Postgres: events appended through the repository commit and roll
/// back together with the module DbContext's transaction; FetchForWriting gives optimistic concurrency;
/// archive and history behave as documented. TransactionBehavior itself is unit-tested with a fake
/// participant (SharedKernel.UnitTests); here the REAL participant is driven the way the behavior drives it.
/// </summary>
public sealed class MartenEnlistmentTests(ProbeHostFixture fixture) : IClassFixture<ProbeHostFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_then_load_round_trips_state_and_version()
    {
        var id = Guid.NewGuid();

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            var counter = ProbeCounter.Start(id, Now);
            counter.Increment(5, Now);
            await repo.AddAsync(counter);

            Assert.NotNull(db.Database.CurrentTransaction); // the participant opened the module tx early
            Assert.True(participant.HasPendingChanges);

            await participant.FlushAsync(CancellationToken.None);
            await db.Database.CurrentTransaction!.CommitAsync();

            Assert.Empty(counter.UncommittedEvents);
        }

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var loaded = await repo.LoadAsync(id);

            Assert.NotNull(loaded);
            Assert.Equal(5, loaded.Value);
            Assert.Equal(2, loaded.Version); // two events in the stream
            Assert.Equal(id, loaded.Id);
        }
    }

    [Fact]
    public async Task Load_returns_null_for_an_unknown_stream()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();

        Assert.Null(await repo.LoadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Rollback_of_the_module_transaction_discards_events_and_EF_rows_together()
    {
        var id = Guid.NewGuid();

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            await repo.AddAsync(ProbeCounter.Start(id, Now));
            db.Rows.Add(new ProbeRow { Id = id, Name = "doomed" });
            await db.SaveChangesAsync();
            await participant.FlushAsync(CancellationToken.None);

            await db.Database.CurrentTransaction!.RollbackAsync();
            participant.Discard();
        }

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            Assert.Null(await repo.LoadAsync(id));
            Assert.False(await db.Rows.AnyAsync(r => r.Id == id));
        }
    }

    [Fact]
    public async Task Concurrent_writer_surfaces_as_ConcurrencyConflictException()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var first = fixture.Host.Services.CreateAsyncScope();
        await using var second = fixture.Host.Services.CreateAsyncScope();

        var repo1 = first.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var repo2 = second.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var counter1 = (await repo1.LoadAsync(id))!;
        var counter2 = (await repo2.LoadAsync(id))!;

        counter1.Increment(1, Now);
        await repo1.SaveAsync(counter1);
        await first.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
        await first.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.CommitAsync();

        counter2.Increment(1, Now);
        await repo2.SaveAsync(counter2);
        var participant2 = second.ServiceProvider.GetRequiredService<ITransactionParticipant>();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => participant2.FlushAsync(CancellationToken.None).AsTask());

        await second.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.RollbackAsync();
    }

    [Fact]
    public async Task Domain_events_are_taken_from_tracked_aggregates_and_cleared_after_flush()
    {
        var id = Guid.NewGuid();
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();

        var counter = ProbeCounter.Start(id, Now);
        await repo.AddAsync(counter);
        await participant.FlushAsync(CancellationToken.None);

        var events = participant.TakeDomainEvents().ToList();

        Assert.Single(events);
        Assert.IsType<CounterStarted>(events[0]);
        Assert.Empty(counter.UncommittedEvents);
        Assert.Empty(participant.TakeDomainEvents());
        await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.RollbackAsync();
    }

    [Fact]
    public async Task History_exposes_versions_metadata_and_typed_data()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, increments: 2);

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();

        var history = await repo.HistoryAsync(id);

        Assert.Equal(3, history.Count);
        Assert.Equal([1L, 2L, 3L], history.Select(h => h.Version));
        Assert.IsType<CounterStarted>(history[0].Data);
        Assert.All(history, h => Assert.False(string.IsNullOrEmpty(h.EventType)));
        Assert.All(history, h => Assert.True(h.Sequence > 0));
    }

    [Fact]
    public async Task Retire_archives_the_stream_so_default_queries_exclude_it()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var counter = (await repo.LoadAsync(id))!;
            counter.Retire(Now);
            await repo.SaveAsync(counter);
            await scope.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.CommitAsync();
        }

        var store = fixture.Host.Services.GetRequiredService<IProbeEventStore>();
        await using var query = store.QuerySession();
        var state = await query.Events.FetchStreamStateAsync(id);

        Assert.NotNull(state);
        Assert.True(state.IsArchived);
    }

    private async Task SeedAsync(Guid id, int increments = 0)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var counter = ProbeCounter.Start(id, Now);
        for (var i = 0; i < increments; i++)
        {
            counter.Increment(1, Now);
        }

        await repo.AddAsync(counter);
        await scope.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.CommitAsync();
    }
}
```

- [ ] **Step 5: Run to verify failure**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests`
Expected: compile error — `MartenEventSourcedRepository` missing.

- [ ] **Step 6: Implement the repository base**

`MartenEventSourcedRepository.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Ardalis.GuardClauses;
using Marten;
using Marten.Events;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Marten-backed base for bespoke event-sourced repositories
/// (<c>AccountRepository : MartenEventSourcedRepository&lt;Account, ILedgerEventStore&gt;, IAccountRepository</c>).
/// <para>
/// <see cref="LoadAsync"/> uses <c>FetchForWriting</c>, which records the stream's current version so a
/// concurrent append is detected at flush (→ <c>ConcurrencyConflictException</c>). Unregistered aggregate
/// types are built by Marten's <i>live aggregation</i> — replaying the stream through the aggregate's
/// <c>Apply(TEvent)</c> methods — so the write model needs no projection registration. The stream version
/// is stamped from <c>IEventStream.CurrentVersion</c>, not from a property convention.
/// </para>
/// </summary>
public abstract class MartenEventSourcedRepository<TAggregate, TStore>(IModuleEventStoreSession<TStore> session)
    : IEventSourcedRepository<TAggregate>
    where TAggregate : EventSourcedAggregate
    where TStore : IDocumentStore
{
    private readonly IModuleEventStoreSession<TStore> _session = session;
    private readonly Dictionary<Guid, IEventStream<TAggregate>> _streams = [];

    /// <summary>For projection queries in derived repositories (<c>Session.OpenQuerySession()</c>).</summary>
    protected IModuleEventStoreSession<TStore> Session => _session;

    public async Task<TAggregate?> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guard.Against.Default(id);

        var documentSession = await _session.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        var stream = await documentSession.Events.FetchForWriting<TAggregate>(id, cancellationToken).ConfigureAwait(false);

        if (stream.Aggregate is null)
        {
            return null;
        }

        _streams[id] = stream;
        IEventSourcedAggregate aggregate = stream.Aggregate;
        aggregate.SetVersion(stream.CurrentVersion ?? 0);
        _session.Track(aggregate);
        return stream.Aggregate;
    }

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(aggregate);
        Guard.Against.Default(aggregate.Id, message: "An event-sourced aggregate must set its Id in the first Apply.");
        if (aggregate.UncommittedEvents.Count == 0)
        {
            throw new InvalidOperationException($"{typeof(TAggregate).Name} has no events to start a stream with.");
        }

        var documentSession = await _session.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        documentSession.Events.StartStream<TAggregate>(aggregate.Id, aggregate.UncommittedEvents.Cast<object>().ToArray());
        _session.Track(aggregate);
        _session.MarkPending();
    }

    public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(aggregate);

        if (!_streams.TryGetValue(aggregate.Id, out var stream))
        {
            throw new InvalidOperationException(
                $"{typeof(TAggregate).Name} {aggregate.Id} was not loaded through LoadAsync in this scope. " +
                "Load before saving; use AddAsync for a new aggregate.");
        }

        var documentSession = await _session.GetSessionAsync(cancellationToken).ConfigureAwait(false);

        if (aggregate.UncommittedEvents.Count > 0)
        {
            stream.AppendMany(aggregate.UncommittedEvents.Cast<object>().ToArray());
        }

        if (aggregate.IsMarkedForArchive)
        {
            documentSession.Events.ArchiveStream(aggregate.Id);
        }

        _session.Track(aggregate);
        _session.MarkPending();
    }

    public async Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guard.Against.Default(id);

        await using var query = _session.OpenQuerySession();
        var events = await query.Events.FetchStreamAsync(id, token: cancellationToken).ConfigureAwait(false);

        return events
            .Select(e => new StoredEvent(
                e.Version,
                e.Sequence,
                e.Timestamp,
                e.EventTypeName,
                e.CorrelationId,
                e.Headers is null
                    ? new Dictionary<string, object?>()
                    : e.Headers.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                (IDomainEvent)e.Data))
            .ToList();
    }
}
```

Implementer notes: `IEventStream<T>.CurrentVersion` is `long?` in recent Marten (null for a brand-new stream) — the `?? 0` handles it; if it's a plain `long`, drop the `??`. `FetchStreamAsync` excludes archived events by default; that is intended for history of live streams and is why the archive test checks `FetchStreamStateAsync` instead.

- [ ] **Step 7: Run the integration tests (Docker required)**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests`
Expected: 7 passed. This is the **de-risking gate** for the whole design — if live aggregation of `ProbeCounter` fails (e.g. Marten cannot construct/identify the aggregate), fix it **here** in `EventSourcedAggregate`/the repository (options, in order: ensure the non-public parameterless ctor exists; register `opts.Projections.LiveStreamAggregation<TAggregate>()` inside `AddModuleEventStore` via a generic hook; last resort replay manually with `FetchStreamAsync` + `When`). Record what was needed in the commit body.

- [ ] **Step 8: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.EventSourcing/MartenEventSourcedRepository.cs tests/AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests AllSpice.CleanModularMonolith.slnx
git commit -m "feat(eventsourcing): Marten-backed event-sourced repository base with enlistment integration tests"
```

### Task 7: Ledger module — Domain (`Account` aggregate, events, value objects) + unit tests

**Files:**
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/AllSpice.CleanModularMonolith.Ledger.csproj`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/GlobalUsings.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/MediatorConfiguration.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Application/AssemblyReference.cs` (needed for the csproj to be a valid Mediator assembly; Application content comes in Task 8)
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/ValueObjects/Currency.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/ValueObjects/Money.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/Enums/AccountStatus.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/Exceptions/InsufficientFundsException.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/Events/AccountOpened.cs`, `FundsDeposited.cs`, `FundsWithdrawn.cs`, `AccountClosed.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/Events/Legacy/FundsDepositedV1.cs`, `Legacy/FundsDepositedUpcasts.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Domain/Aggregates/Account.cs`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.csproj`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests/Accounts/AccountTests.cs`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests/Accounts/FundsDepositedUpcastsTests.cs`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests/ValueObjects/MoneyTests.cs`
- Modify: `AllSpice.CleanModularMonolith.slnx`

**Interfaces:**
- Produces (Domain, namespace `AllSpice.CleanModularMonolith.Ledger.Domain.*`):
  - `Currency : SmartEnum<Currency>` — `Aud`, `Usd`, `Eur`; `string Code`; `static bool TryFromCode(string, out Currency)`; `static Currency FromCode(string)` (throws `BusinessRuleViolationException`).
  - `Money : ValueObject` — `decimal Amount`, `Currency Currency`, `static Money Of(decimal, Currency)`, `static Money Zero(Currency)`, `Money Add(Money)`, `Money Subtract(Money)`, `bool IsPositive`.
  - `AccountStatus : SmartEnum<AccountStatus>` — `Open`, `Closed`.
  - events: `AccountOpened(Guid AccountId, Guid OwnerUserId, string Currency, DateTimeOffset OccurredOnUtc)`, `FundsDeposited(Guid AccountId, decimal Amount, string Currency, string Reference, DateTimeOffset OccurredOnUtc)`, `FundsWithdrawn(same shape)`, `AccountClosed(Guid AccountId, DateTimeOffset OccurredOnUtc)` — all `sealed record : IDomainEvent`.
  - `FundsDepositedV1(Guid AccountId, decimal Amount, string Currency, DateTimeOffset OccurredOnUtc)` (plain record, not `IDomainEvent`) + `static FundsDeposited FundsDepositedUpcasts.FromV1(FundsDepositedV1)`.
  - `Account : EventSourcedAggregate` — `Open(Guid accountId, Guid ownerUserId, Currency, DateTimeOffset nowUtc)`, `Deposit(Money, string reference, DateTimeOffset)`, `Withdraw(Money, string reference, DateTimeOffset)`, `Close(DateTimeOffset)`; properties `OwnerUserId`, `Currency`, `Balance`, `Status`, `OpenedUtc`, `ClosedUtc`.
  - `InsufficientFundsException : BusinessRuleViolationException` with `Code == "insufficient_funds"`.

- [ ] **Step 1: Module project skeleton**

`AllSpice.CleanModularMonolith.Ledger.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Ardalis.GuardClauses" />
    <PackageReference Include="Ardalis.Result" />
    <PackageReference Include="Ardalis.SmartEnum" />
    <PackageReference Include="FastEndpoints" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Marten" />
    <PackageReference Include="WolverineFx" />
    <PackageReference Include="WolverineFx.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" />
    <PackageReference Include="Mediator.Abstractions" />
    <PackageReference Include="Mediator.SourceGenerator">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.ApiContracts\AllSpice.CleanModularMonolith.ApiContracts.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.EventSourcing\AllSpice.CleanModularMonolith.EventSourcing.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.Identity.Abstractions\AllSpice.CleanModularMonolith.Identity.Abstractions.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.SharedKernel\AllSpice.CleanModularMonolith.SharedKernel.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.Notifications.Contracts\AllSpice.CleanModularMonolith.Notifications.Contracts.csproj" />
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.Web\AllSpice.CleanModularMonolith.Web.csproj" />
  </ItemGroup>

</Project>
```

`GlobalUsings.cs`:

```csharp
// Project-wide globals for the Ledger module — the template's REFERENCE event-sourced module (ADR-0009).
//
// Convention: Domain and Application files MUST NOT reference any
// AllSpice.CleanModularMonolith.Ledger.Infrastructure.* type, and only Infrastructure may `using Marten`
// (enforced by Architecture.Tests). The Infrastructure namespaces are listed below for the convenience of
// Infrastructure and Api files.

global using Ardalis.GuardClauses;
global using FluentValidation;
global using Mediator;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

// Application layer contracts (safe for all layers to reference)
global using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
global using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;

// Infrastructure-only — used by Infrastructure and Api layers, not Domain/Application.
global using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
global using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Repositories;

// Type aliases
global using AppAssemblyReference = AllSpice.CleanModularMonolith.Ledger.Application.AssemblyReference;
```

(The Infrastructure `global using`s reference namespaces created in Task 9; until then the compiler reports CS0246 for a `global using` of a missing namespace **only if no type in it exists** — so add those two lines in Task 9, not now. For this task include only the first two groups and the alias.)

`MediatorConfiguration.cs`:

```csharp
using Mediator;
using Microsoft.Extensions.DependencyInjection;

[assembly: MediatorOptions(ServiceLifetime = ServiceLifetime.Scoped)]
```

`Application/AssemblyReference.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Application;

/// <summary>
/// Marker type used to scan this module's Application assembly for validators and message handlers.
/// </summary>
internal static class AssemblyReference
{
}
```

Add to solution: `dotnet sln AllSpice.CleanModularMonolith.slnx add Services/AllSpice.CleanModularMonolith.Ledger/AllSpice.CleanModularMonolith.Ledger.csproj --solution-folder Services`

- [ ] **Step 2: Test project + failing tests**

`tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.csproj` — copy `tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests/*.csproj` and change the `ProjectReference` to `..\..\Services\AllSpice.CleanModularMonolith.Ledger\AllSpice.CleanModularMonolith.Ledger.csproj`. Add to solution under `Tests`.

`Accounts/AccountTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.Enums;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Exceptions;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.Accounts;

/// <summary>
/// Given events → When command → Then new events + state. The aggregate is pure: no store, no clock.
/// </summary>
public class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Account OpenAccount(Guid? id = null) => Account.Open(id ?? Guid.NewGuid(), Owner, Currency.Aud, Now);

    [Fact]
    public void Open_raises_AccountOpened_and_sets_initial_state()
    {
        var id = Guid.NewGuid();

        var account = Account.Open(id, Owner, Currency.Aud, Now);

        var opened = Assert.IsType<AccountOpened>(Assert.Single(account.UncommittedEvents));
        Assert.Equal(id, opened.AccountId);
        Assert.Equal(Owner, opened.OwnerUserId);
        Assert.Equal("AUD", opened.Currency);
        Assert.Equal(id, account.Id);
        Assert.Equal(Money.Zero(Currency.Aud), account.Balance);
        Assert.Equal(AccountStatus.Open, account.Status);
        Assert.Equal(Now, account.OpenedUtc);
        Assert.Equal(0, account.Version);
    }

    [Fact]
    public void Open_rejects_default_ids()
    {
        Assert.ThrowsAny<ArgumentException>(() => Account.Open(Guid.Empty, Owner, Currency.Aud, Now));
        Assert.ThrowsAny<ArgumentException>(() => Account.Open(Guid.NewGuid(), Guid.Empty, Currency.Aud, Now));
    }

    [Fact]
    public void Deposit_increases_balance_and_raises_FundsDeposited()
    {
        var account = OpenAccount();

        account.Deposit(Money.Of(150.25m, Currency.Aud), "INV-1", Now);

        Assert.Equal(150.25m, account.Balance.Amount);
        var deposited = Assert.IsType<FundsDeposited>(account.UncommittedEvents[^1]);
        Assert.Equal(150.25m, deposited.Amount);
        Assert.Equal("INV-1", deposited.Reference);
        Assert.Equal("AUD", deposited.Currency);
    }

    [Fact]
    public void Deposit_rejects_non_positive_amount_and_currency_mismatch()
    {
        var account = OpenAccount();

        Assert.Throws<BusinessRuleViolationException>(() => account.Deposit(Money.Zero(Currency.Aud), "x", Now));
        Assert.Throws<BusinessRuleViolationException>(() => account.Deposit(Money.Of(10, Currency.Usd), "x", Now));
    }

    [Fact]
    public void Withdraw_reduces_balance()
    {
        var account = OpenAccount();
        account.Deposit(Money.Of(100, Currency.Aud), "d", Now);

        account.Withdraw(Money.Of(40, Currency.Aud), "w", Now);

        Assert.Equal(60m, account.Balance.Amount);
        Assert.IsType<FundsWithdrawn>(account.UncommittedEvents[^1]);
    }

    [Fact]
    public void Withdraw_more_than_balance_throws_InsufficientFunds_with_stable_code()
    {
        var account = OpenAccount();
        account.Deposit(Money.Of(10, Currency.Aud), "d", Now);

        var ex = Assert.Throws<InsufficientFundsException>(() => account.Withdraw(Money.Of(10.01m, Currency.Aud), "w", Now));

        Assert.Equal("insufficient_funds", ex.Code);
        Assert.Equal(10m, account.Balance.Amount); // no state change on failure
        Assert.Equal(2, account.UncommittedEvents.Count);
    }

    [Fact]
    public void Close_requires_zero_balance_then_marks_for_archive()
    {
        var account = OpenAccount();
        account.Deposit(Money.Of(5, Currency.Aud), "d", Now);

        Assert.Throws<BusinessRuleViolationException>(() => account.Close(Now));

        account.Withdraw(Money.Of(5, Currency.Aud), "w", Now);
        account.Close(Now.AddMinutes(1));

        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.Equal(Now.AddMinutes(1), account.ClosedUtc);
        Assert.True(account.IsMarkedForArchive);
        Assert.IsType<AccountClosed>(account.UncommittedEvents[^1]);
    }

    [Fact]
    public void Closed_account_rejects_further_movements()
    {
        var account = OpenAccount();
        account.Close(Now);

        Assert.Throws<BusinessRuleViolationException>(() => account.Deposit(Money.Of(1, Currency.Aud), "d", Now));
        Assert.Throws<BusinessRuleViolationException>(() => account.Withdraw(Money.Of(1, Currency.Aud), "w", Now));
    }

    [Fact]
    public void Replaying_the_same_events_through_Apply_yields_identical_state()
    {
        var id = Guid.NewGuid();
        var original = Account.Open(id, Owner, Currency.Aud, Now);
        original.Deposit(Money.Of(100, Currency.Aud), "d", Now);
        original.Withdraw(Money.Of(30, Currency.Aud), "w", Now);

        // Marten builds aggregates by calling the public Apply methods on a fresh instance — mimic that.
        var replayed = (Account)Activator.CreateInstance(typeof(Account), nonPublic: true)!;
        foreach (var e in original.UncommittedEvents)
        {
            switch (e)
            {
                case AccountOpened o: replayed.Apply(o); break;
                case FundsDeposited d: replayed.Apply(d); break;
                case FundsWithdrawn w: replayed.Apply(w); break;
            }
        }

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(original.Balance, replayed.Balance);
        Assert.Equal(original.Status, replayed.Status);
        Assert.Empty(replayed.UncommittedEvents); // Apply never records — only Raise does
    }
}
```

`Accounts/FundsDepositedUpcastsTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.Accounts;

public class FundsDepositedUpcastsTests
{
    [Fact]
    public void FromV1_carries_all_fields_and_fills_the_missing_reference()
    {
        var when = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var v1 = new FundsDepositedV1(Guid.Parse("22222222-2222-2222-2222-222222222222"), 12.5m, "AUD", when);

        FundsDeposited upcast = FundsDepositedUpcasts.FromV1(v1);

        Assert.Equal(v1.AccountId, upcast.AccountId);
        Assert.Equal(12.5m, upcast.Amount);
        Assert.Equal("AUD", upcast.Currency);
        Assert.Equal(when, upcast.OccurredOnUtc);
        Assert.Equal(FundsDepositedUpcasts.LegacyReference, upcast.Reference);
    }
}
```

`ValueObjects/MoneyTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Equality_is_by_amount_and_currency()
    {
        Assert.Equal(Money.Of(10, Currency.Aud), Money.Of(10, Currency.Aud));
        Assert.NotEqual(Money.Of(10, Currency.Aud), Money.Of(10, Currency.Usd));
    }

    [Fact]
    public void Add_and_Subtract_require_same_currency()
    {
        var sum = Money.Of(10, Currency.Aud).Add(Money.Of(2.5m, Currency.Aud));
        Assert.Equal(12.5m, sum.Amount);

        Assert.Throws<BusinessRuleViolationException>(() => Money.Of(1, Currency.Aud).Add(Money.Of(1, Currency.Eur)));
    }

    [Fact]
    public void Of_rejects_negative_amounts()
    {
        Assert.ThrowsAny<ArgumentException>(() => Money.Of(-1, Currency.Aud));
    }

    [Theory]
    [InlineData("aud", true)]
    [InlineData("USD", true)]
    [InlineData("XXX", false)]
    public void Currency_TryFromCode_is_case_insensitive(string code, bool expected)
    {
        Assert.Equal(expected, Currency.TryFromCode(code, out _));
    }
}
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests`
Expected: compile errors — Domain types missing.

- [ ] **Step 4: Value objects, enum, exception**

`Domain/ValueObjects/Currency.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;

/// <summary>Closed set of supported ledger currencies (ISO 4217 numeric value, alpha code).</summary>
public sealed class Currency : SmartEnum<Currency>
{
    public static readonly Currency Aud = new(nameof(Aud), 36, "AUD");
    public static readonly Currency Usd = new(nameof(Usd), 840, "USD");
    public static readonly Currency Eur = new(nameof(Eur), 978, "EUR");

    private Currency(string name, int value, string code)
        : base(name, value)
    {
        Code = code;
    }

    /// <summary>ISO 4217 alpha code — what events store, so the stream never depends on this type's layout.</summary>
    public string Code { get; }

    public static bool TryFromCode(string? code, out Currency currency)
    {
        currency = List.FirstOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return currency is not null;
    }

    public static Currency FromCode(string code) =>
        TryFromCode(code, out var currency)
            ? currency
            : throw new BusinessRuleViolationException($"Unsupported currency '{code}'.");
}
```

`Domain/ValueObjects/Money.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;
using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;

/// <summary>An amount in a single currency. Immutable; arithmetic is currency-checked.</summary>
public sealed class Money : ValueObject
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsPositive => Amount > 0;

    public static Money Of(decimal amount, Currency currency)
    {
        Guard.Against.Null(currency);
        Guard.Against.Negative(amount);
        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, Guard.Against.Null(currency));

    public Money Add(Money other) => new(Amount + SameCurrency(other).Amount, Currency);

    public Money Subtract(Money other) => new(Amount - SameCurrency(other).Amount, Currency);

    private Money SameCurrency(Money other)
    {
        Guard.Against.Null(other);
        if (other.Currency != Currency)
        {
            throw new BusinessRuleViolationException($"Currency mismatch: {Currency.Code} vs {other.Currency.Code}.");
        }

        return other;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency.Code;
    }

    public override string ToString() => $"{Amount:0.00} {Currency.Code}";
}
```

`Domain/Enums/AccountStatus.cs`:

```csharp
using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Enums;

public sealed class AccountStatus : SmartEnum<AccountStatus>
{
    public static readonly AccountStatus Open = new(nameof(Open), 1);
    public static readonly AccountStatus Closed = new(nameof(Closed), 2);

    private AccountStatus(string name, int value)
        : base(name, value)
    {
    }
}
```

`Domain/Exceptions/InsufficientFundsException.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Exceptions;

/// <summary>A withdrawal exceeded the available balance. 422 with code <c>insufficient_funds</c>.</summary>
public sealed class InsufficientFundsException(Guid accountId, decimal balance, decimal requested)
    : BusinessRuleViolationException(
        $"Account {accountId} has a balance of {balance:0.00} but {requested:0.00} was requested.")
{
    public Guid AccountId { get; } = accountId;
    public decimal Balance { get; } = balance;
    public decimal Requested { get; } = requested;

    public override string Code => "insufficient_funds";
}
```

- [ ] **Step 5: Events**

One file each under `Domain/Events/` (all `using AllSpice.CleanModularMonolith.SharedKernel.Events;`, namespace `AllSpice.CleanModularMonolith.Ledger.Domain.Events`):

```csharp
/// <summary>A ledger account was opened for a user. Stored name: <c>account_opened</c>.</summary>
public sealed record AccountOpened(Guid AccountId, Guid OwnerUserId, string Currency, DateTimeOffset OccurredOnUtc) : IDomainEvent;
```
```csharp
/// <summary>Money credited to the account. Stored name: <c>funds_deposited_v2</c> (v1 lacked <see cref="Reference"/>; see Legacy).</summary>
public sealed record FundsDeposited(Guid AccountId, decimal Amount, string Currency, string Reference, DateTimeOffset OccurredOnUtc) : IDomainEvent;
```
```csharp
/// <summary>Money debited from the account. Stored name: <c>funds_withdrawn</c>.</summary>
public sealed record FundsWithdrawn(Guid AccountId, decimal Amount, string Currency, string Reference, DateTimeOffset OccurredOnUtc) : IDomainEvent;
```
```csharp
/// <summary>Terminal event; the stream is archived when it is saved. Stored name: <c>account_closed</c>.</summary>
public sealed record AccountClosed(Guid AccountId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
```

`Domain/Events/Legacy/FundsDepositedV1.cs` (namespace `…Domain.Events.Legacy`):

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;

/// <summary>
/// The ORIGINAL shape of <c>FundsDeposited</c> (stored name <c>funds_deposited</c>), kept only so old rows can
/// still be deserialised and upcast. Never raised; not an <c>IDomainEvent</c>. This is the template's worked
/// example of event versioning — see ADR-0009 and AGENTS.md "Evolve an event".
/// </summary>
public sealed record FundsDepositedV1(Guid AccountId, decimal Amount, string Currency, DateTimeOffset OccurredOnUtc);
```

`Domain/Events/Legacy/FundsDepositedUpcasts.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;

/// <summary>Pure transformation from legacy event shapes to the current one. Registered with the store in Infrastructure.</summary>
public static class FundsDepositedUpcasts
{
    /// <summary>Reference used for deposits recorded before references were captured.</summary>
    public const string LegacyReference = "legacy-import";

    public static FundsDeposited FromV1(FundsDepositedV1 v1) =>
        new(v1.AccountId, v1.Amount, v1.Currency, LegacyReference, v1.OccurredOnUtc);
}
```

- [ ] **Step 6: The aggregate**

`Domain/Aggregates/Account.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.Enums;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Exceptions;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;

/// <summary>
/// A money ledger account — the template's reference event-sourced aggregate (ADR-0009). It is a textbook
/// fit: every movement must be auditable, "balance as of" questions are natural, and the events themselves
/// are the business record. Command methods guard invariants and <c>Raise</c>; <c>Apply</c> methods are pure
/// state transitions used both here and by the store when it replays the stream.
/// </summary>
public sealed class Account : EventSourcedAggregate
{
    // Non-public parameterless ctor: used by the store to build the aggregate before replaying events.
    private Account()
    {
    }

    /// <summary>Local user UUID (User.Id) — never the Keycloak external id (ADR-0005).</summary>
    public Guid OwnerUserId { get; private set; }

    public Currency Currency { get; private set; } = Currency.Aud;

    public Money Balance { get; private set; } = Money.Zero(Currency.Aud);

    public AccountStatus Status { get; private set; } = AccountStatus.Open;

    public DateTimeOffset OpenedUtc { get; private set; }

    public DateTimeOffset? ClosedUtc { get; private set; }

    public static Account Open(Guid accountId, Guid ownerUserId, Currency currency, DateTimeOffset nowUtc)
    {
        Guard.Against.Default(accountId);
        Guard.Against.Default(ownerUserId);
        Guard.Against.Null(currency);

        var account = new Account();
        account.Raise(new AccountOpened(accountId, ownerUserId, currency.Code, nowUtc));
        return account;
    }

    public void Deposit(Money amount, string reference, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        EnsureCurrency(amount);
        Guard.Against.NullOrWhiteSpace(reference);
        if (!amount.IsPositive)
        {
            throw new BusinessRuleViolationException("Deposit amount must be positive.");
        }

        Raise(new FundsDeposited(Id, amount.Amount, amount.Currency.Code, reference.Trim(), nowUtc));
    }

    public void Withdraw(Money amount, string reference, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        EnsureCurrency(amount);
        Guard.Against.NullOrWhiteSpace(reference);
        if (!amount.IsPositive)
        {
            throw new BusinessRuleViolationException("Withdrawal amount must be positive.");
        }

        if (Balance.Amount < amount.Amount)
        {
            throw new InsufficientFundsException(Id, Balance.Amount, amount.Amount);
        }

        Raise(new FundsWithdrawn(Id, amount.Amount, amount.Currency.Code, reference.Trim(), nowUtc));
    }

    public void Close(DateTimeOffset nowUtc)
    {
        EnsureOpen();
        if (Balance.IsPositive)
        {
            throw new BusinessRuleViolationException("An account with a non-zero balance cannot be closed.");
        }

        Raise(new AccountClosed(Id, nowUtc));
        MarkForArchive(); // terminal: the stream is archived at the next save (never deleted)
    }

    // ---- State transitions (store replay convention). No validation here, ever. ----

    public void Apply(AccountOpened e)
    {
        Id = e.AccountId;
        OwnerUserId = e.OwnerUserId;
        Currency = Currency.FromCode(e.Currency);
        Balance = Money.Zero(Currency);
        Status = AccountStatus.Open;
        OpenedUtc = e.OccurredOnUtc;
    }

    public void Apply(FundsDeposited e) => Balance = Balance.Add(Money.Of(e.Amount, Currency));

    public void Apply(FundsWithdrawn e) => Balance = Balance.Subtract(Money.Of(e.Amount, Currency));

    public void Apply(AccountClosed e)
    {
        Status = AccountStatus.Closed;
        ClosedUtc = e.OccurredOnUtc;
    }

    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountOpened e: Apply(e); break;
            case FundsDeposited e: Apply(e); break;
            case FundsWithdrawn e: Apply(e); break;
            case AccountClosed e: Apply(e); break;
            default: throw new InvalidOperationException($"{nameof(Account)} cannot apply {@event.GetType().Name}.");
        }
    }

    private void EnsureOpen()
    {
        if (Status != AccountStatus.Open)
        {
            throw new BusinessRuleViolationException($"Account {Id} is {Status.Name.ToLowerInvariant()}.");
        }
    }

    private void EnsureCurrency(Money amount)
    {
        Guard.Against.Null(amount);
        if (amount.Currency != Currency)
        {
            throw new BusinessRuleViolationException($"Account {Id} is denominated in {Currency.Code}, not {amount.Currency.Code}.");
        }
    }
}
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests`
Expected: all pass (9 + 1 + 4). `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings (the Ledger csproj currently compiles Domain + the Application marker only).

- [ ] **Step 8: Commit**

```bash
git add Services/AllSpice.CleanModularMonolith.Ledger tests/AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests AllSpice.CleanModularMonolith.slnx
git commit -m "feat(ledger): event-sourced Account aggregate, events, Money/Currency, legacy upcast (domain)"
```

---

### Task 8: Ledger module — Application (commands, queries, repository contract, domain-event handler) + unit tests

**Files:**
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Application/Contracts/Persistence/IAccountRepository.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Application/DTOs/AccountSummaryDto.cs`, `AccountHistoryEntryDto.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Application/Features/Accounts/Commands/OpenAccount/OpenAccountCommand.cs`, `OpenAccountCommandHandler.cs`, `OpenAccountCommandValidator.cs`
- Create: `…/Commands/DepositFunds/DepositFundsCommand.cs`, `DepositFundsCommandHandler.cs`, `DepositFundsCommandValidator.cs`
- Create: `…/Commands/WithdrawFunds/WithdrawFundsCommand.cs`, `WithdrawFundsCommandHandler.cs`, `WithdrawFundsCommandValidator.cs`
- Create: `…/Commands/CloseAccount/CloseAccountCommand.cs`, `CloseAccountCommandHandler.cs`, `CloseAccountCommandValidator.cs`
- Create: `…/Queries/GetAccountSummary/GetAccountSummaryQuery.cs`, `GetAccountSummaryQueryHandler.cs`, `GetAccountSummaryQueryValidator.cs`
- Create: `…/Queries/GetAccountHistory/GetAccountHistoryQuery.cs`, `GetAccountHistoryQueryHandler.cs`, `GetAccountHistoryQueryValidator.cs`
- Create: `…/Features/Accounts/Events/AccountOpenedDomainEventHandler.cs`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.csproj`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests/Accounts/DepositFundsCommandHandlerTests.cs`, `OpenAccountCommandHandlerTests.cs`, `AccountOpenedDomainEventHandlerTests.cs`, `GetAccountHistoryQueryHandlerTests.cs`

**Interfaces:**
- Consumes: Task 7 Domain; `IEventSourcedRepository<Account>`, `StoredEvent`, `ITransactional`, `IIntegrationEventPublisher`, `NotificationRequestedIntegrationEvent`.
- Produces:
  - `IAccountRepository : IEventSourcedRepository<Account> { Task<AccountSummaryDto?> GetSummaryAsync(Guid accountId, CancellationToken ct = default); }`
  - `AccountSummaryDto(Guid AccountId, Guid OwnerUserId, string Currency, decimal Balance, string Status, long Version, DateTimeOffset LastActivityUtc)`
  - `AccountHistoryEntryDto(long Version, string EventType, DateTimeOffset Timestamp, string? CorrelationId, string? IdempotencyKey, object Data)`
  - Commands: `OpenAccountCommand(Guid OwnerUserId, string Currency) : IRequest<Result<Guid>>, ITransactional`; `DepositFundsCommand(Guid AccountId, decimal Amount, string Reference) : IRequest<Result>, ITransactional`; `WithdrawFundsCommand(same) `; `CloseAccountCommand(Guid AccountId) : IRequest<Result>, ITransactional`.
  - Queries: `GetAccountSummaryQuery(Guid AccountId) : IRequest<Result<AccountSummaryDto>>`; `GetAccountHistoryQuery(Guid AccountId) : IRequest<Result<IReadOnlyList<AccountHistoryEntryDto>>>`.

- [ ] **Step 1: Test project + failing tests**

Csproj: copy `tests/AllSpice.CleanModularMonolith.Notifications.Application.UnitTests/*.csproj` (it includes Moq and `<Using Include="Moq" />` — verify and keep both), pointing the `ProjectReference` at the Ledger csproj. Add to the solution under `Tests`.

`Accounts/OpenAccountCommandHandlerTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class OpenAccountCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private readonly Mock<IAccountRepository> _repository = new();
    private readonly OpenAccountCommandHandler _handler;

    public OpenAccountCommandHandlerTests()
    {
        _handler = new OpenAccountCommandHandler(_repository.Object, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Adds_a_new_account_and_returns_its_id()
    {
        Account? added = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback((Account a, CancellationToken _) => added = a)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new OpenAccountCommand(Guid.NewGuid(), "aud"), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.NotNull(added);
        Assert.Equal(added.Id, result.Value);
        Assert.Equal("AUD", added.Currency.Code);
        Assert.Equal(Now, added.OpenedUtc);
    }
}
```

`Accounts/DepositFundsCommandHandlerTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class DepositFundsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private readonly Mock<IAccountRepository> _repository = new();
    private readonly DepositFundsCommandHandler _handler;

    public DepositFundsCommandHandlerTests()
    {
        _handler = new DepositFundsCommandHandler(_repository.Object, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Deposits_into_a_loaded_account_and_saves_it()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), Currency.Aud, Now);
        _repository.Setup(r => r.LoadAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var result = await _handler.Handle(new DepositFundsCommand(account.Id, 25m, "INV-9"), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(25m, account.Balance.Amount);
        _repository.Verify(r => r.SaveAsync(account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_NotFound_when_the_account_does_not_exist()
    {
        _repository.Setup(r => r.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        var result = await _handler.Handle(new DepositFundsCommand(Guid.NewGuid(), 1m, "x"), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        _repository.Verify(r => r.SaveAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

`Accounts/AccountOpenedDomainEventHandlerTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class AccountOpenedDomainEventHandlerTests
{
    [Fact]
    public async Task Publishes_an_in_app_notification_request_for_the_owner()
    {
        var publisher = new Mock<IIntegrationEventPublisher>();
        NotificationRequestedIntegrationEvent? published = null;
        publisher.Setup(p => p.PublishAsync(It.IsAny<NotificationRequestedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback((NotificationRequestedIntegrationEvent e, CancellationToken _) => published = e)
            .Returns(ValueTask.CompletedTask);
        var handler = new AccountOpenedDomainEventHandler(publisher.Object);
        var owner = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        await handler.Handle(new AccountOpened(accountId, owner, "AUD", DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal("Ledger", published.SourceModule);
        Assert.Equal(owner.ToString(), published.RecipientUserId);
        Assert.Equal(NotificationChannel.InApp, published.Channel);
        Assert.Equal(accountId.ToString(), published.Metadata!["accountId"]);
    }
}
```

`Accounts/GetAccountHistoryQueryHandlerTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class GetAccountHistoryQueryHandlerTests
{
    [Fact]
    public async Task Maps_stored_events_including_the_idempotency_header()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IAccountRepository>();
        repository.Setup(r => r.HistoryAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new StoredEvent(1, 10, DateTimeOffset.UnixEpoch, "account_opened", "corr-1",
                new Dictionary<string, object?> { ["idempotency-key"] = "key-1" },
                new AccountOpened(id, Guid.NewGuid(), "AUD", DateTimeOffset.UnixEpoch)),
        ]);
        var handler = new GetAccountHistoryQueryHandler(repository.Object);

        var result = await handler.Handle(new GetAccountHistoryQuery(id), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        var entry = Assert.Single(result.Value);
        Assert.Equal(1, entry.Version);
        Assert.Equal("account_opened", entry.EventType);
        Assert.Equal("corr-1", entry.CorrelationId);
        Assert.Equal("key-1", entry.IdempotencyKey);
        Assert.IsType<AccountOpened>(entry.Data);
    }

    [Fact]
    public async Task Returns_NotFound_for_an_empty_stream()
    {
        var repository = new Mock<IAccountRepository>();
        repository.Setup(r => r.HistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var handler = new GetAccountHistoryQueryHandler(repository.Object);

        var result = await handler.Handle(new GetAccountHistoryQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
```

The repo has no fake `TimeProvider` and this does not warrant a package; the tests above use this helper, added to the test project as `FixedTimeProvider.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests;

/// <summary>Deterministic clock for handler tests (ADR-0006: domain time always comes from TimeProvider).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

`GetAccountHistory/GetAccountHistoryQueryValidator.cs`:

```csharp
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;

public sealed class GetAccountHistoryQueryValidator : AbstractValidator<GetAccountHistoryQuery>
{
    public GetAccountHistoryQueryValidator() => RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests`
Expected: compile errors — Application types missing.

- [ ] **Step 3: Repository contract and DTOs**

`Application/Contracts/Persistence/IAccountRepository.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;

/// <summary>
/// Bespoke repository for the event-sourced <see cref="Account"/> (golden rule 4). Writes go through the
/// inherited stream API; reads of current state go through the inline <c>AccountSummary</c> projection —
/// never by replaying the stream in a query.
/// </summary>
public interface IAccountRepository : IEventSourcedRepository<Account>
{
    /// <summary>Current-state read model, or <c>null</c> when the account does not exist.</summary>
    Task<AccountSummaryDto?> GetSummaryAsync(Guid accountId, CancellationToken cancellationToken = default);
}
```

`Application/DTOs/AccountSummaryDto.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Application.DTOs;

public sealed record AccountSummaryDto(
    Guid AccountId,
    Guid OwnerUserId,
    string Currency,
    decimal Balance,
    string Status,
    long Version,
    DateTimeOffset LastActivityUtc);
```

`Application/DTOs/AccountHistoryEntryDto.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Application.DTOs;

/// <summary>One audit-trail entry: stream position, stored type name, store timestamp, request metadata, payload.</summary>
public sealed record AccountHistoryEntryDto(
    long Version,
    string EventType,
    DateTimeOffset Timestamp,
    string? CorrelationId,
    string? IdempotencyKey,
    object Data);
```

- [ ] **Step 4: Commands**

`OpenAccount/OpenAccountCommand.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;

/// <param name="OwnerUserId">Local user UUID (User.Id) — not the Keycloak external id.</param>
/// <param name="Currency">ISO 4217 alpha code (AUD, USD, EUR).</param>
public sealed record OpenAccountCommand(Guid OwnerUserId, string Currency) : IRequest<Result<Guid>>, ITransactional;
```

`OpenAccount/OpenAccountCommandHandler.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;

public sealed class OpenAccountCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<OpenAccountCommand, Result<Guid>>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result<Guid>> Handle(OpenAccountCommand request, CancellationToken cancellationToken)
    {
        var account = Account.Open(Guid.NewGuid(), request.OwnerUserId, Currency.FromCode(request.Currency), _timeProvider.GetUtcNow());
        await _accounts.AddAsync(account, cancellationToken);
        return Result.Success(account.Id);
    }
}
```

`OpenAccount/OpenAccountCommandValidator.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;

public sealed class OpenAccountCommandValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEqual(Guid.Empty).WithMessage("OwnerUserId must be a non-empty local user UUID.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(code => Currency.TryFromCode(code, out _))
            .WithMessage(_ => $"Unsupported currency. Supported: {string.Join(", ", Currency.List.Select(c => c.Code))}.");
    }
}
```

`DepositFunds/DepositFundsCommand.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;

public sealed record DepositFundsCommand(Guid AccountId, decimal Amount, string Reference) : IRequest<Result>, ITransactional;
```

`DepositFunds/DepositFundsCommandHandler.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;

public sealed class DepositFundsCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<DepositFundsCommand, Result>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(DepositFundsCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.LoadAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound($"Account {request.AccountId} was not found.");
        }

        // Money is denominated in the account's own currency; a mismatch is impossible from this API by design.
        account.Deposit(Money.Of(request.Amount, account.Currency), request.Reference, _timeProvider.GetUtcNow());
        await _accounts.SaveAsync(account, cancellationToken);
        return Result.Success();
    }
}
```

`DepositFunds/DepositFundsCommandValidator.cs`:

```csharp
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;

public sealed class DepositFundsCommandValidator : AbstractValidator<DepositFundsCommand>
{
    public DepositFundsCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}
```

`WithdrawFunds/*` — identical shape to DepositFunds with `Withdraw` instead of `Deposit`:

```csharp
public sealed record WithdrawFundsCommand(Guid AccountId, decimal Amount, string Reference) : IRequest<Result>, ITransactional;
```
```csharp
public sealed class WithdrawFundsCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<WithdrawFundsCommand, Result>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(WithdrawFundsCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.LoadAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound($"Account {request.AccountId} was not found.");
        }

        account.Withdraw(Money.Of(request.Amount, account.Currency), request.Reference, _timeProvider.GetUtcNow());
        await _accounts.SaveAsync(account, cancellationToken);
        return Result.Success();
    }
}
```
```csharp
public sealed class WithdrawFundsCommandValidator : AbstractValidator<WithdrawFundsCommand>
{
    public WithdrawFundsCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}
```
(same `using`s and namespace pattern `…Commands.WithdrawFunds`).

`CloseAccount/*`:

```csharp
public sealed record CloseAccountCommand(Guid AccountId) : IRequest<Result>, ITransactional;
```
```csharp
public sealed class CloseAccountCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<CloseAccountCommand, Result>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.LoadAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound($"Account {request.AccountId} was not found.");
        }

        account.Close(_timeProvider.GetUtcNow());
        await _accounts.SaveAsync(account, cancellationToken); // archives the stream (MarkForArchive)
        return Result.Success();
    }
}
```
```csharp
public sealed class CloseAccountCommandValidator : AbstractValidator<CloseAccountCommand>
{
    public CloseAccountCommandValidator() => RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
}
```

- [ ] **Step 5: Queries**

`GetAccountSummary/GetAccountSummaryQuery.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;

public sealed record GetAccountSummaryQuery(Guid AccountId) : IRequest<Result<AccountSummaryDto>>;
```

`GetAccountSummary/GetAccountSummaryQueryHandler.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;

public sealed class GetAccountSummaryQueryHandler(IAccountRepository accounts)
    : IRequestHandler<GetAccountSummaryQuery, Result<AccountSummaryDto>>
{
    private readonly IAccountRepository _accounts = accounts;

    public async ValueTask<Result<AccountSummaryDto>> Handle(GetAccountSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _accounts.GetSummaryAsync(request.AccountId, cancellationToken);
        return summary is null
            ? Result<AccountSummaryDto>.NotFound($"Account {request.AccountId} was not found.")
            : Result.Success(summary);
    }
}
```

`GetAccountSummary/GetAccountSummaryQueryValidator.cs`:

```csharp
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;

public sealed class GetAccountSummaryQueryValidator : AbstractValidator<GetAccountSummaryQuery>
{
    public GetAccountSummaryQueryValidator() => RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
}
```

`GetAccountHistory/GetAccountHistoryQuery.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;

public sealed record GetAccountHistoryQuery(Guid AccountId) : IRequest<Result<IReadOnlyList<AccountHistoryEntryDto>>>;
```

`GetAccountHistory/GetAccountHistoryQueryHandler.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;

/// <summary>The audit-trail read: the raw stream with store metadata. Lists of accounts must use a projection instead.</summary>
public sealed class GetAccountHistoryQueryHandler(IAccountRepository accounts)
    : IRequestHandler<GetAccountHistoryQuery, Result<IReadOnlyList<AccountHistoryEntryDto>>>
{
    private const string IdempotencyKeyHeader = "idempotency-key"; // mirrors EventSourcing.EventHeaders (Application must not reference Marten's project)

    private readonly IAccountRepository _accounts = accounts;

    public async ValueTask<Result<IReadOnlyList<AccountHistoryEntryDto>>> Handle(GetAccountHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _accounts.HistoryAsync(request.AccountId, cancellationToken);
        if (history.Count == 0)
        {
            return Result<IReadOnlyList<AccountHistoryEntryDto>>.NotFound($"Account {request.AccountId} was not found.");
        }

        IReadOnlyList<AccountHistoryEntryDto> entries = history
            .Select(e => new AccountHistoryEntryDto(
                e.Version,
                e.EventType,
                e.Timestamp,
                e.CorrelationId,
                e.Headers.TryGetValue(IdempotencyKeyHeader, out var key) ? key?.ToString() : null,
                e.Data))
            .ToList();

        return Result.Success(entries);
    }
}
```

`GetAccountHistory/GetAccountHistoryQueryValidator.cs` — defined under Step 1 above (one `NotEqual(Guid.Empty)` rule).

- [ ] **Step 6: Domain-event handler → existing integration event**

`Features/Accounts/Events/AccountOpenedDomainEventHandler.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Events;

/// <summary>
/// Stream events ARE domain events, so this handler runs inside the OpenAccount transaction (drain loop) and
/// the integration event it publishes enrols the same outbox transaction: the account, its projection and
/// the notification request commit or roll back together. Reuses the Notifications contract — no new
/// Contracts project is needed to talk to an existing module.
/// </summary>
public sealed class AccountOpenedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<AccountOpened>
{
    private readonly IIntegrationEventPublisher _publisher = publisher;

    public async ValueTask Handle(AccountOpened notification, CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(new NotificationRequestedIntegrationEvent(
            EventId: Guid.NewGuid(),
            SourceModule: "Ledger",
            RecipientUserId: notification.OwnerUserId.ToString(),
            RecipientEmail: null,
            RecipientPhoneNumber: null,
            Channel: NotificationChannel.InApp,
            Subject: "Ledger account opened",
            Body: $"Your {notification.Currency} ledger account is ready.",
            TemplateKey: null,
            ScheduledSendUtc: null,
            CorrelationId: null,
            Metadata: new Dictionary<string, string> { ["accountId"] = notification.AccountId.ToString() }), cancellationToken);
    }
}
```

- [ ] **Step 7: Run tests and build**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests` → 6 passed. `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add Services/AllSpice.CleanModularMonolith.Ledger/Application tests/AllSpice.CleanModularMonolith.Ledger.Application.UnitTests AllSpice.CleanModularMonolith.slnx Directory.Packages.props
git commit -m "feat(ledger): account commands, queries, repository contract and AccountOpened → notification handler"
```

### Task 9: Ledger module — Infrastructure (DbContext, Marten store, projection, repository, module extensions) + integration tests

**Files:**
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Persistence/ILedgerEventStore.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Persistence/LedgerDbContext.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Persistence/LedgerDbContextDesignTimeFactory.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Persistence/LedgerEventStoreConfiguration.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Projections/AccountSummary.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Projections/AccountSummaryProjection.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Repositories/AccountRepository.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Authorization/LedgerPermissionManifest.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Extensions/LedgerModuleExtensions.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Infrastructure/Migrations/*` (generated by `dotnet ef`)
- Modify: `Services/AllSpice.CleanModularMonolith.Ledger/GlobalUsings.cs` (add the two Infrastructure `global using`s)
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests.csproj`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests/LedgerHostFixture.cs`
- Create: `tests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests/AccountRepositoryTests.cs`

**Interfaces:**
- Consumes: Tasks 5–8.
- Produces:
  - `ILedgerEventStore : IDocumentStore`
  - `LedgerDbContext : DbContext, IModuleDbContext` (no entities; Wolverine envelope mapping only)
  - `static class LedgerEventStoreConfiguration { const string SchemaName = "ledger"; static void Configure(StoreOptions opts); }`
  - `AccountSummary` document + `AccountSummaryProjection`
  - `AccountRepository : MartenEventSourcedRepository<Account, ILedgerEventStore>, IAccountRepository`
  - `LedgerPermissionManifest` with keys `ledger.access`, `ledger:accounts.read`, `ledger:accounts.write`
  - `IHostApplicationBuilder AddLedgerModuleServices(this IHostApplicationBuilder, ILogger)`, `Task<WebApplication> EnsureLedgerModuleDatabaseAsync(this WebApplication)`; database resource name `ledgerdb`.

- [ ] **Step 1: Persistence types**

`Infrastructure/Persistence/ILedgerEventStore.cs`:

```csharp
using Marten;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// Marker for the Ledger module's Marten store (registered with <c>AddMartenStore&lt;ILedgerEventStore&gt;</c>).
/// Lives in <c>ledgerdb</c>, schema <c>ledger</c>, and enlists in <see cref="LedgerDbContext"/>'s transaction.
/// </summary>
public interface ILedgerEventStore : IDocumentStore
{
}
```

`Infrastructure/Persistence/LedgerDbContext.cs`:

```csharp
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// The Ledger module's EF Core context. It has NO entities of its own — the only aggregate is event-sourced —
/// but it still exists because it (1) owns the module transaction that the Marten session enlists in and
/// (2) hosts the module's co-located Wolverine outbox tables. A real module would add its ordinary EF
/// aggregates here alongside the event-sourced ones.
/// </summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options), IModuleDbContext
{
    DbContext IModuleDbContext.Instance => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Co-locate the Wolverine durable outbox tables in this module's own database so integration
        // events commit in the SAME transaction as the events appended to the Marten store.
        if (Database.IsNpgsql())
        {
            modelBuilder.MapWolverineEnvelopeStorage("wolverine");
        }
    }
}
```

`Infrastructure/Persistence/LedgerDbContextDesignTimeFactory.cs` — copy `NotificationsDbContextDesignTimeFactory.cs`, rename the class/context to `Ledger`, set `DatabaseName = "ledgerdb"`, env var `EF_DESIGN_LEDGER_CONNECTION`, and the error text accordingly.

`Infrastructure/Persistence/LedgerEventStoreConfiguration.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;
using JasperFx.Events.Projections;
using Marten;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// Everything Marten needs to know about the Ledger stream: projections, stored event-type names and
/// upcasters. Kept separate from DI so the integration tests can build a store with exactly the production
/// configuration.
/// </summary>
public static class LedgerEventStoreConfiguration
{
    public const string SchemaName = "ledger";

    public static void Configure(StoreOptions opts)
    {
        // Current-state read model, updated in the same transaction as the append.
        opts.Projections.Add(new AccountSummaryProjection(), ProjectionLifecycle.Inline);

        // Event versioning (the template's worked example — ADR-0009):
        //  * the CURRENT shape keeps the canonical CLR name but gets a NEW stored name so it never collides
        //    with rows written under the old shape;
        //  * the OLD shape is kept as a *V1 CLR type and upcast on read. Marten's default naming would map
        //    both FundsDeposited and FundsDepositedV1 to snake_case names that don't match history, so both
        //    are pinned explicitly.
        opts.Events.MapEventType<FundsDeposited>("funds_deposited_v2");
        opts.Events.Upcast<FundsDepositedV1, FundsDeposited>("funds_deposited", FundsDepositedUpcasts.FromV1);
    }
}
```

- [ ] **Step 2: Projection**

`Infrastructure/Projections/AccountSummary.cs`:

```csharp
namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;

/// <summary>
/// Inline single-stream projection document: the account's current state for reads. <see cref="Version"/> is
/// the stream version the document reflects (an "as of" marker for clients and for concurrency-aware UIs).
/// </summary>
public sealed class AccountSummary
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset LastActivityUtc { get; set; }
}
```

`Infrastructure/Projections/AccountSummaryProjection.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Domain.Enums;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;

/// <summary>
/// Builds <see cref="AccountSummary"/> from the stream. Uses <c>IEvent&lt;T&gt;</c> overloads where the stream
/// version is needed (requires <c>EventAppendMode.Rich</c>, set by <c>AddModuleEventStore</c>).
/// </summary>
public sealed class AccountSummaryProjection : SingleStreamProjection<AccountSummary, Guid>
{
    public static AccountSummary Create(IEvent<AccountOpened> e) => new()
    {
        Id = e.Data.AccountId,
        OwnerUserId = e.Data.OwnerUserId,
        Currency = e.Data.Currency,
        Balance = 0m,
        Status = AccountStatus.Open.Name,
        Version = e.Version,
        LastActivityUtc = e.Data.OccurredOnUtc,
    };

    public static void Apply(IEvent<FundsDeposited> e, AccountSummary summary)
    {
        summary.Balance += e.Data.Amount;
        summary.Version = e.Version;
        summary.LastActivityUtc = e.Data.OccurredOnUtc;
    }

    public static void Apply(IEvent<FundsWithdrawn> e, AccountSummary summary)
    {
        summary.Balance -= e.Data.Amount;
        summary.Version = e.Version;
        summary.LastActivityUtc = e.Data.OccurredOnUtc;
    }

    public static void Apply(IEvent<AccountClosed> e, AccountSummary summary)
    {
        summary.Status = AccountStatus.Closed.Name;
        summary.Version = e.Version;
        summary.LastActivityUtc = e.Data.OccurredOnUtc;
    }
}
```

Implementer note: if Marten 9.33 requires the conventional methods to be instance rather than static, or `Create` to take the plain event, follow the compiler/runtime error — the shape (document fields, `Version` from `e.Version`) is what matters.

- [ ] **Step 3: Repository, manifest, module extensions**

`Infrastructure/Repositories/AccountRepository.cs`:

```csharp
using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Repositories;

public sealed class AccountRepository(IModuleEventStoreSession<ILedgerEventStore> session)
    : MartenEventSourcedRepository<Account, ILedgerEventStore>(session), IAccountRepository
{
    public async Task<AccountSummaryDto?> GetSummaryAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var query = Session.OpenQuerySession();
        var summary = await query.LoadAsync<AccountSummary>(accountId, cancellationToken);
        return summary is null
            ? null
            : new AccountSummaryDto(summary.Id, summary.OwnerUserId, summary.Currency, summary.Balance,
                summary.Status, summary.Version, summary.LastActivityUtc);
    }
}
```

`Infrastructure/Authorization/LedgerPermissionManifest.cs`:

```csharp
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Authorization;

/// <summary>Every permission key the Ledger module enforces; seeded as IsSystem by the reconciler.</summary>
public sealed class LedgerPermissionManifest : IModulePermissionManifest
{
    public string ModuleKey => "ledger";

    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("ledger.access", "Access the ledger module"),
        new("ledger:accounts.read", "View ledger accounts and their history"),
        new("ledger:accounts.write", "Open, close and move funds on ledger accounts"),
    ];
}
```

`Infrastructure/Extensions/LedgerModuleExtensions.cs`:

```csharp
using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.HealthChecks;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Extensions;

/// <summary>
/// Wires the Ledger module — the template's reference EVENT-SOURCED module (ADR-0009) — into the host.
/// Everything is the standard module recipe plus one call: <c>AddModuleEventStore</c>.
/// </summary>
public static class LedgerModuleExtensions
{
    private const string DatabaseResourceName = "ledgerdb";

    public static IHostApplicationBuilder AddLedgerModuleServices(this IHostApplicationBuilder builder, ILogger logger)
    {
        builder.Services.AddSingleton<IModulePermissionManifest, LedgerPermissionManifest>();

        var connectionString = builder.Configuration[$"ConnectionStrings:{DatabaseResourceName}"]
            ?? throw new InvalidOperationException($"Connection string '{DatabaseResourceName}' is required for the Ledger module.");

        // Same registration as the other modules: the context owns the transaction and hosts the co-located
        // outbox; shared-kernel interceptors are attached explicitly (EF Core does not discover them from DI).
        builder.Services.AddDbContextWithWolverineIntegration<LedgerDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));
        builder.EnrichNpgsqlDbContext<LedgerDbContext>(settings =>
        {
            settings.DisableRetry = true;          // user-initiated transactions forbid the retrying strategy
            settings.DisableHealthChecks = true;   // DbContextHealthCheck<LedgerDbContext> below covers it
        });
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());

        // The opt-in: a Marten store in ledgerdb (schema "ledger") enlisted in LedgerDbContext's transaction.
        builder.AddModuleEventStore<ILedgerEventStore, LedgerDbContext>(
            connectionString,
            LedgerEventStoreConfiguration.SchemaName,
            LedgerEventStoreConfiguration.Configure);

        builder.Services.AddScoped<IAccountRepository, AccountRepository>();

        builder.Services.AddMediator();
        builder.Services.AddValidatorsFromAssembly(typeof(AppAssemblyReference).Assembly);

        builder.Services.AddHealthChecks()
            .AddCheck<DbContextHealthCheck<LedgerDbContext>>("ledger-db");

        logger.LogInformation("Ledger module services registered (event-sourced Account via Marten)");
        return builder;
    }

    /// <summary>EF migration (outbox tables) under the advisory lock, then the Marten schema for the event store.</summary>
    public static async Task<WebApplication> EnsureLedgerModuleDatabaseAsync(this WebApplication app)
    {
        await MigrationRunner.RunForModuleAsync<LedgerDbContext>(app.Services, app.Lifetime, loggerCategory: "LedgerDatabase");
        await app.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>(app.Lifetime.ApplicationStopping);
        return app;
    }
}
```

Now add to `GlobalUsings.cs` the two Infrastructure lines from Task 7's listing (`…Ledger.Infrastructure.Persistence` and `…Ledger.Infrastructure.Repositories`).

- [ ] **Step 4: Build, then generate the initial migration**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings.

Ensure the EF tool exists: `dotnet ef --version` (if missing: `dotnet tool install --global dotnet-ef`). Then (the design-time factory needs a password value but `migrations add` never connects):

```bash
EF_DESIGN_DB_PASSWORD=design-only dotnet ef migrations add InitialCreate \
  --project Services/AllSpice.CleanModularMonolith.Ledger/AllSpice.CleanModularMonolith.Ledger.csproj \
  --startup-project AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
  --context LedgerDbContext --output-dir Infrastructure/Migrations
```

Expected: `Infrastructure/Migrations/<timestamp>_InitialCreate.cs` + `.Designer.cs` + `LedgerDbContextModelSnapshot.cs` containing only the `wolverine.*` envelope tables. (The startup project is the gateway, which does not yet reference Ledger — if `dotnet ef` fails to locate the context, temporarily use `--startup-project Services/AllSpice.CleanModularMonolith.Ledger/…csproj` — the design-time factory makes the module project self-sufficient.) Rebuild → 0 warnings.

- [ ] **Step 5: Integration tests (Testcontainers)**

Csproj: copy the EventSourcing.IntegrationTests csproj from Task 6; change project references to the Ledger csproj and the EventSourcing csproj; add `<PackageReference Include="Moq" />`. Add to solution under `Tests`.

`LedgerHostFixture.cs`:

```csharp
using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests;

/// <summary>
/// Wires the Ledger persistence exactly as production does (same store configuration, same repository) minus
/// Wolverine/Aspire, against a throwaway Postgres.
/// </summary>
public sealed class LedgerHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public IHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = _postgres.GetConnectionString();

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(cs));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());
        builder.AddModuleEventStore<ILedgerEventStore, LedgerDbContext>(cs, LedgerEventStoreConfiguration.SchemaName, LedgerEventStoreConfiguration.Configure);
        builder.Services.AddScoped<IAccountRepository, AccountRepository>();
        Host = builder.Build();

        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();
        }

        await Host.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>();
        await Host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await _postgres.DisposeAsync();
    }
}
```

`AccountRepositoryTests.cs`:

```csharp
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests;

public sealed class AccountRepositoryTests(LedgerHostFixture fixture) : IClassFixture<LedgerHostFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 11, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public async Task Inline_projection_matches_the_stream_after_commit()
    {
        var id = await OpenAsync(deposits: [100m, 50m], withdrawals: [30m]);

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var summary = await repo.GetSummaryAsync(id);

        Assert.NotNull(summary);
        Assert.Equal(120m, summary.Balance);
        Assert.Equal("AUD", summary.Currency);
        Assert.Equal("Open", summary.Status);
        Assert.Equal(4, summary.Version); // opened + 2 deposits + 1 withdrawal
        Assert.Equal(Owner, summary.OwnerUserId);
    }

    [Fact]
    public async Task Summary_is_null_for_unknown_account()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        Assert.Null(await repo.GetSummaryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Closing_archives_the_stream_and_the_summary_reads_Closed()
    {
        var id = await OpenAsync();

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var account = (await repo.LoadAsync(id))!;
            account.Close(Now);
            await repo.SaveAsync(account);
            await CommitAsync(scope);
        }

        await using var verify = fixture.Host.Services.CreateAsyncScope();
        var summary = await verify.ServiceProvider.GetRequiredService<IAccountRepository>().GetSummaryAsync(id);
        Assert.Equal("Closed", summary!.Status);

        await using var query = fixture.Host.Services.GetRequiredService<ILedgerEventStore>().QuerySession();
        Assert.True((await query.Events.FetchStreamStateAsync(id))!.IsArchived);
    }

    [Fact]
    public async Task Legacy_funds_deposited_rows_upcast_on_load_and_in_history()
    {
        var id = await OpenAsync();

        // Write a row under the OLD stored name the way a pre-versioning deployment would have.
        var store = fixture.Host.Services.GetRequiredService<ILedgerEventStore>();
        await using (var session = store.LightweightSession())
        {
            session.Events.Append(id, new FundsDepositedV1(id, 42m, "AUD", Now));
            await session.SaveChangesAsync();
        }

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account = (await repo.LoadAsync(id))!;
        Assert.Equal(42m, account.Balance.Amount);

        var history = await repo.HistoryAsync(id);
        var upcast = Assert.IsType<FundsDeposited>(history[^1].Data);
        Assert.Equal(FundsDepositedUpcasts.LegacyReference, upcast.Reference);
        Assert.Equal("funds_deposited", history[^1].EventType);

        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.CurrentTransaction!.RollbackAsync();
    }

    [Fact]
    public async Task Current_deposits_are_stored_under_the_v2_name()
    {
        var id = await OpenAsync(deposits: [1m]);

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var history = await scope.ServiceProvider.GetRequiredService<IAccountRepository>().HistoryAsync(id);

        Assert.Equal("funds_deposited_v2", history[^1].EventType);
    }

    [Fact]
    public async Task Schema_apply_is_idempotent()
    {
        await fixture.Host.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>();
        await fixture.Host.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>();
    }

    private async Task<Guid> OpenAsync(decimal[]? deposits = null, decimal[]? withdrawals = null)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Open(Guid.NewGuid(), Owner, Currency.Aud, Now);
        foreach (var d in deposits ?? []) account.Deposit(Money.Of(d, Currency.Aud), "seed", Now);
        foreach (var w in withdrawals ?? []) account.Withdraw(Money.Of(w, Currency.Aud), "seed", Now);
        await repo.AddAsync(account);
        await CommitAsync(scope);
        return account.Id;
    }

    private static async Task CommitAsync(AsyncServiceScope scope)
    {
        await scope.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.CurrentTransaction!.CommitAsync();
    }
}
```

Implementer note on the legacy test: if Marten refuses to append a type that is only registered as an upcast source, insert the row with SQL instead — `INSERT INTO ledger.mt_events (seq_id, id, stream_id, version, data, type, timestamp, tenant_id, mt_dotnet_type, is_archived)` is version-specific; the simplest robust alternative is `opts.Events.AddEventType<FundsDepositedV1>()` **in the test's** store configuration only (wrap `LedgerEventStoreConfiguration.Configure` and add it), never in production config.

- [ ] **Step 6: Run**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests` → 6 passed. `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add Services/AllSpice.CleanModularMonolith.Ledger tests/AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests AllSpice.CleanModularMonolith.slnx
git commit -m "feat(ledger): Marten store config, AccountSummary inline projection, repository, module wiring, migrations"
```

---

### Task 10: Ledger API endpoints, ApiContracts, gateway + AppHost wiring, JasperFx CLI

**Files:**
- Create: `Shared/AllSpice.CleanModularMonolith.ApiContracts/Ledger/Responses/OpenAccountResponse.cs`, `AccountSummaryResponse.cs`, `AccountHistoryEntryResponse.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Api/Endpoints/Accounts/OpenAccountRequest.cs`, `MoneyMovementRequest.cs`
- Create: `Services/AllSpice.CleanModularMonolith.Ledger/Api/Endpoints/Accounts/OpenAccountEndpoint.cs`, `DepositFundsEndpoint.cs`, `WithdrawFundsEndpoint.cs`, `CloseAccountEndpoint.cs`, `GetAccountSummaryEndpoint.cs`, `GetAccountHistoryEndpoint.cs`
- Create: `AllSpice.CleanModularMonolith.ApiGateway/Infrastructure/EventSourcing/HttpEventMetadataProvider.cs`
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj` (reference Ledger + EventSourcing)
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/Extensions/GatewayModuleRegistrationExtensions.cs`
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/Extensions/GatewayServiceCollectionExtensions.cs:25-30`
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/Program.cs`
- Modify: `AllSpice.CleanModularMonolith.AppHost/AppHost.cs:111-117, 226-229`
- Modify: `AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.http`

**Interfaces:**
- Consumes: Tasks 8–9.
- Produces: routes under `/api/ledger/accounts` (see table in spec §6.4, prefixed `/api` to match the template's existing routes); `HttpEventMetadataProvider : IEventMetadataProvider`.

- [ ] **Step 1: Response contracts** (`namespace AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses`)

```csharp
public sealed record OpenAccountResponse(Guid AccountId);
```
```csharp
public sealed record AccountSummaryResponse(
    Guid AccountId, Guid OwnerUserId, string Currency, decimal Balance, string Status, long Version, DateTimeOffset LastActivityUtc);
```
```csharp
/// <summary>One audit-trail entry. <see cref="Data"/> is the stored event payload, serialised as-is.</summary>
public sealed record AccountHistoryEntryResponse(
    long Version, string EventType, DateTimeOffset Timestamp, string? CorrelationId, string? IdempotencyKey, object Data);
```

- [ ] **Step 2: Requests and endpoints** (`namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts`)

`OpenAccountRequest.cs`:

```csharp
public sealed class OpenAccountRequest
{
    /// <summary>Local user UUID (User.Id) that owns the account.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>ISO 4217 alpha code: AUD, USD or EUR.</summary>
    public string Currency { get; set; } = "AUD";
}
```

`MoneyMovementRequest.cs` (route id comes from the URL):

```csharp
public sealed class MoneyMovementRequest
{
    public decimal Amount { get; set; }

    /// <summary>Free-text reference recorded on the event (invoice number, payout id, …). Not personal data.</summary>
    public string Reference { get; set; } = string.Empty;
}
```

`OpenAccountEndpoint.cs`:

```csharp
using AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

public sealed class OpenAccountEndpoint(IMediator mediator) : Endpoint<OpenAccountRequest, OpenAccountResponse>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Post("/api/ledger/accounts");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
        Summary(s => s.Summary = "Opens an event-sourced ledger account (reference implementation, ADR-0009).");
    }

    public override async Task HandleAsync(OpenAccountRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new OpenAccountCommand(req.OwnerUserId, req.Currency), ct);
        if (result.IsSuccess)
        {
            await TypedResults.Created($"/api/ledger/accounts/{result.Value}", new OpenAccountResponse(result.Value)).ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
```

`DepositFundsEndpoint.cs` (WithdrawFunds is identical with `withdrawals` / `WithdrawFundsCommand`):

```csharp
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

public sealed class DepositFundsEndpoint(IMediator mediator) : Endpoint<MoneyMovementRequest>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Post("/api/ledger/accounts/{accountId:guid}/deposits");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
    }

    public override async Task HandleAsync(MoneyMovementRequest req, CancellationToken ct)
    {
        var accountId = Route<Guid>("accountId");
        var result = await _mediator.Send(new DepositFundsCommand(accountId, req.Amount, req.Reference), ct);
        if (result.IsSuccess)
        {
            await TypedResults.NoContent().ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
```

`CloseAccountEndpoint.cs`:

```csharp
public sealed class CloseAccountEndpoint(IMediator mediator) : EndpointWithoutRequest
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Post("/api/ledger/accounts/{accountId:guid}/close");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseAccountCommand(Route<Guid>("accountId")), ct);
        if (result.IsSuccess)
        {
            await TypedResults.NoContent().ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
```

`GetAccountSummaryEndpoint.cs`:

```csharp
public sealed class GetAccountSummaryEndpoint(IMediator mediator) : EndpointWithoutRequest<AccountSummaryResponse>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Get("/api/ledger/accounts/{accountId:guid}");
        Policies(PermissionPolicy.For("ledger:accounts.read"));
        Tags("Ledger");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountSummaryQuery(Route<Guid>("accountId")), ct);
        if (result.IsSuccess)
        {
            var s = result.Value;
            await TypedResults.Ok(new AccountSummaryResponse(s.AccountId, s.OwnerUserId, s.Currency, s.Balance, s.Status, s.Version, s.LastActivityUtc))
                .ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
```

`GetAccountHistoryEndpoint.cs`:

```csharp
public sealed class GetAccountHistoryEndpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<AccountHistoryEntryResponse>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Get("/api/ledger/accounts/{accountId:guid}/history");
        Policies(PermissionPolicy.For("ledger:accounts.read"));
        Tags("Ledger");
        Summary(s => s.Summary = "The account's full event stream with store metadata — the audit trail.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountHistoryQuery(Route<Guid>("accountId")), ct);
        if (result.IsSuccess)
        {
            IReadOnlyList<AccountHistoryEntryResponse> entries = result.Value
                .Select(e => new AccountHistoryEntryResponse(e.Version, e.EventType, e.Timestamp, e.CorrelationId, e.IdempotencyKey, e.Data))
                .ToList();
            await TypedResults.Ok(entries).ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
```

(Each endpoint file needs the `using`s shown in the first two; add the query/command namespaces as appropriate.)

- [ ] **Step 3: Gateway metadata provider**

`AllSpice.CleanModularMonolith.ApiGateway/Infrastructure/EventSourcing/HttpEventMetadataProvider.cs`:

```csharp
using AllSpice.CleanModularMonolith.ApiGateway.Middleware;
using AllSpice.CleanModularMonolith.EventSourcing;

namespace AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.EventSourcing;

/// <summary>
/// Stamps every appended event with the request's correlation id (set by <see cref="CorrelationIdMiddleware"/>)
/// and the client's <c>Idempotency-Key</c>, so a stream can be tied back to a request and a replayed command
/// recognised at the stream level. Scoped: read once per request.
/// </summary>
public sealed class HttpEventMetadataProvider(IHttpContextAccessor httpContextAccessor) : IEventMetadataProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[HttpHeaderNames.CorrelationId] as string;

    public string? IdempotencyKey =>
        _httpContextAccessor.HttpContext?.Request.Headers[IdempotencyMiddleware.HeaderName].FirstOrDefault();
}
```

- [ ] **Step 4: Gateway registration**

In `ApiGateway.csproj` add project references to `Services/AllSpice.CleanModularMonolith.Ledger` and `Shared/AllSpice.CleanModularMonolith.EventSourcing` (mirror the existing Notifications/Identity reference lines).

`GatewayModuleRegistrationExtensions.cs`:
1. Right after `builder.Services.AddSharedKernelInterceptors();` add:
   ```csharp
           // Request-scoped event metadata for event-sourced modules. Registered BEFORE the modules so the
           // EventSourcing TryAdd fallback (NullEventMetadataProvider) does not win.
           builder.Services.AddScoped<IEventMetadataProvider, HttpEventMetadataProvider>();
   ```
   with `using AllSpice.CleanModularMonolith.EventSourcing;` and `using AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.EventSourcing;`.
2. After `builder.AddIdentityModuleServices(logger);` add `builder.AddLedgerModuleServices(logger);` (`using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Extensions;`).
3. Add `var ledgerConnectionString = builder.Configuration.GetConnectionString("ledgerdb");` to the connection-string block, include it in the null check, and extend the error message to name `ledgerdb`.
4. In `UseWolverine`, after the notifications ancillary store:
   ```csharp
               opts.PersistMessagesWithPostgresql(ledgerConnectionString, "wolverine", MessageStoreRole.Ancillary)
                   .Enroll<LedgerDbContext>();
   ```
   (`using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;`).

`GatewayServiceCollectionExtensions.cs` — add to `o.Assemblies`:
```csharp
                    typeof(AllSpice.CleanModularMonolith.Ledger.Infrastructure.Extensions.LedgerModuleExtensions).Assembly,
```

- [ ] **Step 5: `Program.cs` — Ledger ensure + JasperFx command runner**

- After `builder.Host.UseSerilog(...)` add `builder.Host.ApplyJasperFxExtensions();` (`using JasperFx;`).
- After `await app.EnsureIdentityModuleDatabaseAsync();` add `await app.EnsureLedgerModuleDatabaseAsync();` (`using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Extensions;`).
- Replace `app.Run();` with:
  ```csharp
    // JasperFx command runner: `dotnet run` with no args starts the host exactly as before; with args it runs a
    // maintenance command instead — e.g. `dotnet run -- projections rebuild` (Marten) or `codegen write`
    // (Wolverine). See ARCHITECTURE.md "Event sourcing".
    return await app.RunJasperFxCommands(args);
  ```
  The top-level program now returns `int`; the `catch` rethrows and `finally` flushes, so all paths still terminate correctly.

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings. Then verify the CLI is alive without a database: `dotnet run --project AllSpice.CleanModularMonolith.ApiGateway -- help` should list commands including `projections` (it will fail fast if the connection-string guard runs first — if so, that is acceptable: document in the commit that the CLI needs the same connection strings as the host).

- [ ] **Step 6: AppHost**

In `AppHost.cs` after `var identityDatabase = postgres.AddDatabase("identitydb");` add:
```csharp
// ledgerdb hosts the Ledger module's Marten event store (schema "ledger") AND its co-located outbox.
var ledgerDatabase = postgres.AddDatabase("ledgerdb");
```
and add `.WithReference(ledgerDatabase)` to the `apigateway` project after `.WithReference(identityDatabase)`.

- [ ] **Step 7: `.http` requests**

Append to `AllSpice.CleanModularMonolith.ApiGateway.http`:

```http
### Ledger (event-sourced reference module) — open an account; requires ledger:accounts.write
POST {{host}}/api/ledger/accounts
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "ownerUserId": "11111111-1111-1111-1111-111111111111",
  "currency": "AUD"
}

### Deposit — replace {accountId}; send twice with the same Idempotency-Key to see a replay, not a double deposit
POST {{host}}/api/ledger/accounts/{accountId}/deposits
Authorization: Bearer {{token}}
Content-Type: application/json
Idempotency-Key: ledger-demo-0001

{
  "amount": 100.00,
  "reference": "INV-1001"
}

### Withdraw more than the balance → 422 insufficient_funds
POST {{host}}/api/ledger/accounts/{accountId}/withdrawals
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "amount": 999.00,
  "reference": "PAYOUT-1"
}

### Current state (inline projection)
GET {{host}}/api/ledger/accounts/{accountId}
Authorization: Bearer {{token}}

### Audit trail — the raw event stream with correlation id / idempotency key per event
GET {{host}}/api/ledger/accounts/{accountId}/history
Authorization: Bearer {{token}}
```

- [ ] **Step 8: Build, run the suite, smoke the host**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx && dotnet test AllSpice.CleanModularMonolith.slnx --no-build` → green. Then start the stack (`dotnet run --project AllSpice.CleanModularMonolith.AppHost/AllSpice.CleanModularMonolith.AppHost.csproj`), confirm the gateway logs "Ledger module services registered", `/health` is 200, and the `ledger` schema exists in `ledgerdb` (pgweb from the Aspire dashboard, or `psql -c "\dn"`). Stop the stack.

- [ ] **Step 9: Commit**

```bash
git add Shared/AllSpice.CleanModularMonolith.ApiContracts/Ledger Services/AllSpice.CleanModularMonolith.Ledger/Api AllSpice.CleanModularMonolith.ApiGateway AllSpice.CleanModularMonolith.AppHost/AppHost.cs
git commit -m "feat(ledger,gateway): ledger endpoints, ledgerdb wiring, event metadata provider, JasperFx command runner"
```

### Task 11: Foundation integration test — event-sourced write + outbox envelope are atomic

**Files:**
- Modify: `tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests.csproj` (reference `EventSourcing` + `Marten`)
- Create: `tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests/EventSourcedOutboxAtomicityTests.cs`

**Interfaces:**
- Consumes: `TransactionBehavior` (Task 4), `AddModuleEventStore` + `MartenEventSourcedRepository` (Tasks 5–6), Wolverine `IDbContextOutbox`, existing `ProbeEvent`/`ProbeEventHandler` from `OutboxAtomicityTests.cs`.

- [ ] **Step 1: Add references**

In the Foundation csproj add:
```xml
    <PackageReference Include="Marten" />
```
and
```xml
    <ProjectReference Include="..\..\Shared\AllSpice.CleanModularMonolith.EventSourcing\AllSpice.CleanModularMonolith.EventSourcing.csproj" />
```

- [ ] **Step 2: Write the tests**

```csharp
using System.Collections.Concurrent;
using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// The ADR-0009 guarantee, end to end through the REAL <see cref="TransactionBehavior{TRequest,TResponse}"/>:
/// an event-sourced append, an ordinary EF row and an integration-event envelope commit atomically — and when
/// the handler fails after appending, none of the three exist and nothing is delivered. Mirrors
/// <see cref="OutboxAtomicityTests"/> but drives the behavior instead of a hand-rolled transaction.
/// </summary>
public sealed class EventSourcedOutboxAtomicityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = _postgres.GetConnectionString();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContextWithWolverineIntegration<EsProbeDbContext>(o => o.UseNpgsql(cs));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<EsProbeDbContext>());
        builder.AddModuleEventStore<IEsProbeStore, EsProbeDbContext>(cs, "esprobe");
        builder.Services.AddScoped<TallyRepository>();
        builder.Services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();
        builder.Services.AddScoped<IPostCommitActions, PostCommitActions>();
        builder.Services.AddScoped<IOutboxFlusher, TestOutboxFlusher>();
        builder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(cs, "wolverine");
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(typeof(EventSourcedOutboxAtomicityTests).Assembly);
        });
        _host = builder.Build();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EsProbeDbContext>().Database.EnsureCreatedAsync();
        }

        await _host.Services.ApplyEventStoreSchemaAsync<IEsProbeStore>();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Successful_command_commits_stream_row_and_envelope_together()
    {
        var id = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(id);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var behavior = CreateBehavior(scope);
            var result = await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
            {
                var repo = scope.ServiceProvider.GetRequiredService<TallyRepository>();
                var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
                var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

                await repo.AddAsync(Tally.Start(id), ct);               // opens the module tx early
                db.Marks.Add(new Mark { Id = id, Note = "committed" });
                outbox.Enroll(db);                                        // same shape as WolverineIntegrationEventPublisher
                await outbox.PublishAsync(new ProbeEvent(id));
                return Result.Success();
            }, CancellationToken.None);

            Assert.Equal(ResultStatus.Ok, result.Status);
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(delivered == signal.Task, "Integration event was not delivered after commit.");

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.True(await verify.ServiceProvider.GetRequiredService<EsProbeDbContext>().Marks.AnyAsync(m => m.Id == id));
        Assert.NotNull(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    [Fact]
    public async Task Handler_failure_after_append_persists_nothing_and_delivers_nothing()
    {
        var id = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(id);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var behavior = CreateBehavior(scope);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
                {
                    var repo = scope.ServiceProvider.GetRequiredService<TallyRepository>();
                    var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
                    var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

                    await repo.AddAsync(Tally.Start(id), ct);
                    db.Marks.Add(new Mark { Id = id, Note = "doomed" });
                    outbox.Enroll(db);
                    await outbox.PublishAsync(new ProbeEvent(id));
                    throw new InvalidOperationException("business failure after staging everything");
                }, CancellationToken.None));
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        Assert.False(delivered == signal.Task, "Integration event was delivered despite a failed command.");

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<EsProbeDbContext>().Marks.AnyAsync(m => m.Id == id));
        Assert.Null(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    [Fact]
    public async Task Failure_result_after_append_persists_nothing()
    {
        var id = Guid.NewGuid();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var behavior = CreateBehavior(scope);
            var result = await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
            {
                await scope.ServiceProvider.GetRequiredService<TallyRepository>().AddAsync(Tally.Start(id), ct);
                return Result.Conflict("changed my mind");
            }, CancellationToken.None);

            Assert.Equal(ResultStatus.Conflict, result.Status);
        }

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.Null(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    private static TransactionBehavior<EsProbeCommand, Result> CreateBehavior(AsyncServiceScope scope) =>
        new(scope.ServiceProvider.GetServices<IModuleDbContext>(),
            scope.ServiceProvider.GetServices<ITransactionParticipant>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>(),
            scope.ServiceProvider.GetServices<IOutboxFlusher>(),
            scope.ServiceProvider.GetRequiredService<IPostCommitActions>(),
            NullLogger<TransactionBehavior<EsProbeCommand, Result>>.Instance);

    private sealed record EsProbeCommand : IMessage, ITransactional;

    /// <summary>Releases the outbox after commit, like the gateway's WolverineOutboxFlusher.</summary>
    private sealed class TestOutboxFlusher(IDbContextOutbox outbox) : IOutboxFlusher
    {
        public async ValueTask FlushAsync(CancellationToken cancellationToken) => await outbox.FlushOutgoingMessagesAsync();
    }
}

public sealed record TallyStarted(Guid TallyId) : IDomainEvent;

public sealed class Tally : EventSourcedAggregate
{
    private Tally() { }

    public static Tally Start(Guid id)
    {
        var tally = new Tally();
        tally.Raise(new TallyStarted(id));
        return tally;
    }

    public void Apply(TallyStarted e) => Id = e.TallyId;

    protected override void When(IDomainEvent @event)
    {
        if (@event is TallyStarted e) Apply(e);
        else throw new InvalidOperationException($"Unhandled {@event.GetType().Name}");
    }
}

public interface IEsProbeStore : Marten.IDocumentStore { }

public sealed class TallyRepository(IModuleEventStoreSession<IEsProbeStore> session)
    : MartenEventSourcedRepository<Tally, IEsProbeStore>(session);

public sealed class Mark
{
    public Guid Id { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class EsProbeDbContext(DbContextOptions<EsProbeDbContext> options) : DbContext(options), IModuleDbContext
{
    public DbSet<Mark> Marks => Set<Mark>();
    public DbContext Instance => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mark>().HasKey(m => m.Id);
        modelBuilder.MapWolverineEnvelopeStorage("wolverine");
    }
}
```

`NoOpDomainEventDispatcher` already exists in `TwoModuleHost.cs` (internal, same assembly). Check `IOutboxFlusher.FlushAsync`'s exact signature in SharedKernel and match it.

- [ ] **Step 3: Run**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests --filter "FullyQualifiedName~EventSourcedOutboxAtomicityTests"` → 3 passed.

- [ ] **Step 4: Commit**

```bash
git add tests/AllSpice.CleanModularMonolith.Foundation.IntegrationTests
git commit -m "test(foundation): event-sourced append, EF row and outbox envelope commit and roll back atomically"
```

---

### Task 12: Architecture-fitness rules for event sourcing and the Ledger module

**Files:**
- Modify: `tests/AllSpice.CleanModularMonolith.Architecture.Tests/AllSpice.CleanModularMonolith.Architecture.Tests.csproj` (reference Ledger + EventSourcing)
- Modify: `tests/AllSpice.CleanModularMonolith.Architecture.Tests/ArchitectureRulesTests.cs`
- Modify: `tests/AllSpice.CleanModularMonolith.Architecture.Tests/Authorization/PermissionKeyConsistencyTests.cs:30-34`

- [ ] **Step 1: Add references and the Ledger assembly**

Add `ProjectReference`s for `Services/AllSpice.CleanModularMonolith.Ledger` and `Shared/AllSpice.CleanModularMonolith.EventSourcing` to the Architecture.Tests csproj.

In `ArchitectureRulesTests.cs` add:

```csharp
    private static readonly Assembly Ledger =
        typeof(AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates.Account).Assembly;
    private static readonly Assembly EventSourcing =
        typeof(AllSpice.CleanModularMonolith.EventSourcing.ModuleEventStoreExtensions).Assembly;

    private const string LedgerRoot = "AllSpice.CleanModularMonolith.Ledger";

    private static Assembly ModuleAssembly(string moduleRoot) => moduleRoot switch
    {
        IdentityRoot => Identity,
        NotificationsRoot => Notifications,
        LedgerRoot => Ledger,
        _ => throw new ArgumentOutOfRangeException(nameof(moduleRoot), moduleRoot, null),
    };
```

Replace every `var assembly = moduleRoot == IdentityRoot ? Identity : Notifications;` with `var assembly = ModuleAssembly(moduleRoot);` and add `[InlineData(LedgerRoot)]` to the three `[Theory]` tests (`Domain_layer_has_no_infrastructure_dependencies`, `Mediator_handlers_live_in_the_Application_layer`, `Aggregate_roots_live_in_the_Domain_layer`).

Add `"Marten"` and `"JasperFx"` to `InfrastructureDependencies`.

- [ ] **Step 2: New rules**

```csharp
    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    [InlineData(LedgerRoot)]
    public void Marten_is_referenced_only_from_the_Infrastructure_layer(string moduleRoot)
    {
        // Event sourcing is an INFRASTRUCTURE choice (ADR-0009). Domain aggregates use SharedKernel's
        // EventSourcedAggregate; Application uses IEventSourcedRepository. Only Infrastructure may see Marten.
        var result = Types.InAssembly(ModuleAssembly(moduleRoot))
            .That().DoNotResideInNamespaceStartingWith($"{moduleRoot}.Infrastructure")
            .ShouldNot().HaveDependencyOnAny("Marten", "JasperFx", "Weasel")
            .GetResult();

        AssertSuccess(result, $"Only {moduleRoot}.Infrastructure may depend on Marten");
    }

    [Fact]
    public void SharedKernel_has_no_dependency_on_Marten()
    {
        var result = Types.InAssembly(SharedKernel)
            .ShouldNot().HaveDependencyOnAny("Marten", "JasperFx", "Weasel")
            .GetResult();

        AssertSuccess(result, "SharedKernel must stay store-agnostic; Marten lives in the EventSourcing project");
    }

    [Fact]
    public void Ledger_module_does_not_depend_on_other_module_internals()
    {
        var result = Types.InAssembly(Ledger)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{IdentityRoot}.Application", $"{IdentityRoot}.Infrastructure", $"{IdentityRoot}.Domain",
                $"{NotificationsRoot}.Application", $"{NotificationsRoot}.Infrastructure", $"{NotificationsRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Ledger may reach other modules only via *.Contracts / Identity.Abstractions");
    }

    [Fact]
    public void Other_modules_do_not_depend_on_Ledger()
    {
        foreach (var assembly in new[] { Identity, Notifications })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(LedgerRoot)
                .GetResult();

            AssertSuccess(result, $"{assembly.GetName().Name} must not depend on the Ledger reference module");
        }
    }

    [Theory]
    [InlineData(LedgerRoot)]
    public void Event_sourced_aggregates_live_in_the_Domain_layer(string moduleRoot)
    {
        var result = Types.InAssembly(ModuleAssembly(moduleRoot))
            .That().Inherit(typeof(AllSpice.CleanModularMonolith.SharedKernel.EventSourcing.EventSourcedAggregate))
            .Should().ResideInNamespaceStartingWith($"{moduleRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Event-sourced aggregates belong in the Domain layer");
    }
```

Extend `Domain_events_are_sealed` so stream events (records implementing `IDomainEvent` directly) are covered too, and include Ledger:

```csharp
    [Fact]
    public void Domain_events_are_sealed()
    {
        foreach (var assembly in new[] { SharedKernel, Identity, Notifications, Ledger })
        {
            // Persisted stream events are a permanent contract; sealing prevents accidental subclass drift.
            var result = Types.InAssembly(assembly)
                .That().ImplementInterface(typeof(IDomainEvent))
                .And().AreNotAbstract()
                .Should().BeSealed()
                .GetResult();

            AssertSuccess(result, $"Domain events in {assembly.GetName().Name} must be sealed");
        }
    }
```

In `PermissionKeyConsistencyTests.ModuleAssemblies` add:
```csharp
        typeof(AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates.Account).Assembly,
```

- [ ] **Step 3: Run**

Run: `dotnet test tests/AllSpice.CleanModularMonolith.Architecture.Tests` → all pass. If `Domain_layer_has_no_infrastructure_dependencies` fails for Ledger, the offender is a Domain type touching Marten/JasperFx — fix the Domain type, never the rule.

- [ ] **Step 4: Commit**

```bash
git add tests/AllSpice.CleanModularMonolith.Architecture.Tests
git commit -m "test(architecture): Marten confined to Infrastructure, Ledger isolation, sealed stream events"
```

---

### Task 13: Documentation, ADR 0009, template metadata, review checklist

**Files:**
- Create: `docs/adr/0009-opt-in-event-sourcing-marten-enlisted.md`
- Modify: `docs/adr/README.md`, `.template.config/template.json`, `README.md`, `ARCHITECTURE.md`, `AGENTS.md`, `GETTING_STARTED.md`, `CLAUDE.md`, `TODOS.md`, `deploy/README.md`, `.claude/skills/allspice-clean-review/checklist.md`

- [ ] **Step 1: ADR 0009**

```markdown
# 0009 — Opt-in event sourcing per aggregate, Marten enlisted in the module transaction

- Status: Accepted
- Date: 2026-09-13

## Context

Some aggregates are better modelled as an append-only history than as a mutable row: ledgers, payments,
approvals — anything where "what happened, in what order, and what did it look like on date X" is a
first-class requirement. The template offered only state-based EF Core persistence, so teams either bolted
on ad-hoc audit tables or were tempted to event-source everything. Both are wrong: most aggregates are CRUD
and should stay that way.

Any event store we add must keep the template's core guarantee (ADR-0002): a command's state change and the
integration events it publishes commit **atomically**, in the module's own database.

## Decision

1. **Opt-in per aggregate, inside a normal module.** An aggregate derives from `EventSourcedAggregate`
   (SharedKernel) instead of `Entity`; its stream events are `sealed record … : IDomainEvent` and *are* its
   domain events. Everything else in the module — EF aggregates, outbox, endpoints — is unchanged.
2. **Marten is the event store**, one store per module in that module's database (schema `<module>`).
   Inline single-stream projections provide the read model; the write model is live-aggregated with
   `FetchForWriting` (optimistic concurrency → `409 concurrency_conflict`).
3. **The module `DbContext` stays the transaction owner.** A scoped `MartenTransactionParticipant` opens the
   Marten session *inside* that transaction (`SessionOptions.ForTransaction(tx, shouldAutoCommit: false)`),
   beginning it early if the handler needs a session before commit. `TransactionBehavior` treats
   participants as part of the unit of work: they count toward one-module-per-command, are flushed on each
   drain-loop iteration, contribute domain events, and are discarded on failure. The existing EF-hosted
   Wolverine outbox is untouched, so append + projection + EF row + envelope are one transaction.
4. **Guard rails ship with it:** golden rule 8 (fit criteria) in `AGENTS.md`, architecture tests (Marten only
   in Infrastructure; SharedKernel store-agnostic; stream events sealed), review-checklist items, and the
   `Ledger` reference module showing the shape of a *good* fit — including event versioning (upcaster),
   idempotency/correlation metadata, stream archiving and projection rebuild.

## Alternatives considered

- **`WolverineFx.Marten` / `IntegrateWithWolverine()`** — makes Marten a second unit of work and outbox. A
  handler may target only one ancillary store, and mixing EF and Marten ancillary stores in one host is
  undocumented. That forces *per-module* event sourcing — the opposite of the goal. Rejected.
- **Hand-rolled `events` table on EF Core** — no new dependency, but projections, rebuilds, versioning,
  snapshots and archiving would all be bespoke. Acceptable for one audit stream, not as a template pattern.
- **Event-source everything** — rejected; see golden rule 8.

## Consequences

- Event-sourced commands hold the module transaction open during the handler (early open). Handlers are
  in-memory work plus reads, so the window is short; documented.
- Events are a permanent contract: versioning uses a new stored name + upcaster (`FundsDeposited` →
  `funds_deposited_v2`, `FundsDepositedV1` upcast from `funds_deposited`). No personal data in events;
  crypto-shredding is the escape hatch if that ever changes.
- Marten 9 needs `JasperFx ≥ 2.67`, which required aligning the Wolverine family to 6.36.
- Projection changes need `dotnet run -- projections rebuild` (JasperFx command runner now in `Program.cs`).
- Deferred: async projections/daemon, crypto-shredding helper, a `dotnet new` switch to omit the Ledger sample.
```

Add to `docs/adr/README.md` table:
```markdown
| [0009](0009-opt-in-event-sourcing-marten-enlisted.md) | Opt-in event sourcing per aggregate (Marten enlisted in the module transaction) | Accepted |
```

- [ ] **Step 2: `template.json`**

Add `"Event Sourcing"` to `classifications` and a `description` property after `"name"`:
```json
  "description": "A .NET 10 clean-architecture modular monolith: CQRS (Mediator), FastEndpoints, EF Core + PostgreSQL, Wolverine transactional outbox, Keycloak, Aspire, and opt-in event sourcing (Marten) per aggregate.",
```

- [ ] **Step 3: `README.md`**

- Tagline (line 3): replace "using Clean Architecture, CQRS, and event-driven patterns." with "using Clean Architecture, CQRS, event-driven messaging, and **opt-in event sourcing (Marten)** for the aggregates that need it." and "a complete Identity + Notifications module stack." with "a complete Identity + Notifications module stack plus a small event-sourced **Ledger** reference module."
- Features: after the Wolverine bullet add:
  ```markdown
  - **Opt-in event sourcing (Marten)** — per aggregate, not per module. `EventSourcedAggregate` + a bespoke repository; the Marten session **enlists in the module's EF transaction**, so events, inline projections, EF rows and the outbox envelope commit atomically. Ships with concurrency (409), event versioning via upcasters, idempotency/correlation metadata on every event, stream archiving and `projections rebuild`. Guard-railed by golden rule 8 + architecture tests — see [ADR-0009](docs/adr/0009-opt-in-event-sourcing-marten-enlisted.md)
  ```
- Project Layout: add `|- Services/{{ProjectName}}.Ledger/       -- Event-sourced Account (reference module; deletable)` after Notifications and `|- Shared/{{ProjectName}}.EventSourcing/ -- Marten participant/repository base (only place Marten lives outside module Infrastructure)` after SharedKernel.
- Modules table: add row `| **Ledger** | Reference **event-sourced** module: `Account` aggregate (open/deposit/withdraw/close), Marten store in `ledgerdb` schema `ledger`, inline `AccountSummary` projection, `/history` audit endpoint, `FundsDepositedV1 → FundsDeposited` upcaster, `AccountOpened` → Notifications integration event. Delete it if you don't need a sample — see GETTING_STARTED.md |`.

- [ ] **Step 4: `ARCHITECTURE.md`**

- Key libraries table: add `| Event store (opt-in) | **Marten** — one store per module in the module DB (schema `<module>`), enlisted in the module EF transaction via `MartenTransactionParticipant`; inline projections; `FetchForWriting` concurrency (ADR-0009) |`.
- Module structure: change `Domain/ -- Aggregates, ValueObjects, Enums, Events, Specifications` to add `(event-sourced aggregates derive from EventSourcedAggregate; their Events are the stream)` and Infrastructure to add `Projections` and `(Marten store config lives here)`.
- Shared libraries: add `- **EventSourcing** — `AddModuleEventStore<TStore, TContext>`, `MartenTransactionParticipant`, `MartenEventSourcedRepository`, `IEventMetadataProvider`. The only project outside module Infrastructure layers allowed to reference Marten.`
- Database strategy: append a paragraph: "An event-sourced module additionally has a **Marten schema** (`<module>`) in the same database; `Ensure{Module}ModuleDatabaseAsync` applies it with `ApplyAllConfiguredChangesToDatabaseAsync` after the EF migration (Marten uses its own advisory lock). `AutoCreate.None` at runtime — schema changes are applied at startup, never lazily."
- Replace the "For production cold-start/AOT you can instead pre-generate code (`dotnet run -- codegen write` …)" wording so it states the gateway now runs JasperFx commands (`Program.cs` → `RunJasperFxCommands`).
- New section after "CQRS flow":

```markdown
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
queried through `store.QuerySession()`; lists never replay streams. `HistoryAsync` returns the raw stream with
metadata (version, sequence, timestamp, correlation id, `idempotency-key` header) — the audit trail.

**Concurrency.** A concurrent append fails at flush with Marten's `ConcurrencyException`, translated to
`ConcurrencyConflictException` → `409 concurrency_conflict`. EF's `DbUpdateConcurrencyException` maps to the
same exception.

**Schema & rebuild.** `AddModuleEventStore` fixes: `AutoCreate.None`, `EventAppendMode.Rich`, STJ,
correlation + header metadata, Guid streams. `Ensure{Module}ModuleDatabaseAsync` applies the schema at
startup. Rebuild projections with the JasperFx runner: `dotnet run --project <Gateway> -- projections rebuild`
(or `-p AccountSummary`).

**Versioning.** Keep the old CLR shape as `XxxV1` with its stored name pinned (`MapEventType<FundsDepositedV1>`
is implied by the `Upcast<…>("funds_deposited", …)` registration); give the new shape a new stored name
(`MapEventType<FundsDeposited>("funds_deposited_v2")`); register the upcaster. Never edit or delete events.

**Metadata.** `IEventMetadataProvider` (gateway: `HttpEventMetadataProvider`) stamps `CorrelationId` and the
`Idempotency-Key` on every event. Events carry **no personal data**.

## Ledger module (reference)

`Services/AllSpice.CleanModularMonolith.Ledger` — `Account` (open / deposit / withdraw / close), events
`AccountOpened`, `FundsDeposited` (v2) / `FundsDepositedV1` (legacy, upcast), `FundsWithdrawn`,
`AccountClosed` (archives the stream). `LedgerDbContext` has no entities: it owns the transaction and hosts the
co-located outbox. `AccountOpened` → `NotificationRequestedIntegrationEvent` proves event-sourced write +
outbox atomicity without a new Contracts project. Endpoints under `/api/ledger/accounts` gated by
`ledger:accounts.read|write`. It exists to be copied or deleted — see GETTING_STARTED.md.
```

- Testing & enforcement: add "Marten confined to Infrastructure; SharedKernel store-agnostic; stream events sealed" to the architecture-tests sentence, and "`EventSourcing.IntegrationTests` / `Ledger.Infrastructure.IntegrationTests` (Testcontainers) prove enlistment, concurrency, projection consistency and upcasting".

- [ ] **Step 5: `AGENTS.md`**

- §0 add golden rule 8:
  ```markdown
  8. **Event sourcing is opt-in per aggregate and must be justified.** Default to EF Core. Event-source an
     aggregate only when at least one holds: its history/audit trail is a first-class requirement; temporal
     ("as of") queries are needed; it is a financial/ledger-style append flow; several derived read models are
     needed. **Not a fit:** CRUD/reference data, data mirrored from external systems (Keycloak users), aggregates
     with PII churn, anything where only current state matters. Never put personal data in events. Marten is
     referenced only from a module's `Infrastructure` (architecture test). See ADR-0009 and the `Ledger` module.
  ```
- §1 flow line: add "Event-sourced aggregates: `Handler → bespoke IXxxRepository : IEventSourcedRepository<T> → Marten store enlisted in the module DbContext transaction`."
- §3 Domain: add bullet "**Event-sourced aggregate:** derive from `EventSourcedAggregate`; events are `sealed record … : IDomainEvent` in `Domain/Events`; command methods guard then `Raise(...)`; `public void Apply(TEvent)` methods hold pure state transitions (no validation, no throws); route in `protected override void When(IDomainEvent)` — never name the dispatcher `Apply`. Set `Id` in the first `Apply`. Call `MarkForArchive()` on the terminal event."
- §3 Application: add "Event-sourced: `IXxxRepository : IEventSourcedRepository<Xxx>` + projection read methods; `LoadAsync` → mutate → `SaveAsync`; queries read projections, never the stream (except an explicit history/audit query)."
- §3 Infrastructure: add "Event-sourced: `IXxxEventStore : IDocumentStore` marker; `AddModuleEventStore<IXxxEventStore, XxxDbContext>(cs, "<module>", XxxEventStoreConfiguration.Configure)`; projections in `Infrastructure/Projections`; `XxxRepository : MartenEventSourcedRepository<Xxx, IXxxEventStore>`; `Ensure…` calls `ApplyEventStoreSchemaAsync` after the EF migration."
- §4 DON'T: add
  ```markdown
  - **Don't event-source by default** (golden rule 8), and never store personal data in an event — events cannot be deleted.
  - **Don't edit, delete or re-type stored events.** Add a new stored name + upcaster (`MapEventType`/`Upcast`).
  - **Don't list or search by replaying streams** — build a projection. `HistoryAsync` is for one aggregate's audit trail.
  - **Don't open Marten sessions yourself** (`store.LightweightSession()` in a handler). Go through the repository so the session enlists in the module transaction.
  - **Don't validate inside `Apply`.** Invariants live in command methods; `Apply` must always succeed on replay.
  ```
- §5 recipes: add
  ```markdown
  **Add an event-sourced aggregate** (only if golden rule 8 holds)
  1. Domain: `Xxx : EventSourcedAggregate`, events in `Domain/Events`, `Apply(TEvent)` per event, `When` switch.
  2. Application: `IXxxRepository : IEventSourcedRepository<Xxx>` (+ `GetSummaryAsync`), commands (`ITransactional`) that `LoadAsync` → mutate → `SaveAsync`, queries over the projection.
  3. Infrastructure: `IXxxEventStore : IDocumentStore`; `XxxEventStoreConfiguration.Configure` (projections, `MapEventType`, `Upcast`); `XxxSummary` + `XxxSummaryProjection : SingleStreamProjection<XxxSummary, Guid>`; `XxxRepository : MartenEventSourcedRepository<Xxx, IXxxEventStore>`; in `Add{Module}ModuleServices` call `AddModuleEventStore<…>`; in `Ensure…` call `ApplyEventStoreSchemaAsync<IXxxEventStore>()`.
  4. Tests: Domain given/when/then; Infrastructure integration on Testcontainers (round trip, concurrency, projection, upcast).

  **Evolve an event (upcaster)** — keep the old CLR type as `XxxV1`; give the new shape a new stored name
  `opts.Events.MapEventType<Xxx>("xxx_v2")`; register `opts.Events.Upcast<XxxV1, Xxx>("xxx", XxxUpcasts.FromV1)`;
  unit-test `FromV1`; integration-test that an old row loads as the new type.

  **Rebuild a projection** — `dotnet run --project AllSpice.CleanModularMonolith.ApiGateway -- projections rebuild`
  (or `-p <ProjectionName>`). Requires the same connection strings as the host.
  ```
  Replace the sentence "the template ships only Identity + Notifications (deliberately no sample business domain)" with "the template ships Identity + Notifications plus a deliberately small **Ledger** reference module (the event-sourcing example; delete it if unneeded)".
- §6 utilities table: add rows `| Event-sourced aggregate base | `EventSourcedAggregate` (SharedKernel) |`, `| Event-sourced persistence | `IEventSourcedRepository<T>` + `MartenEventSourcedRepository<T, TStore>` (EventSourcing) |`, `| Register a module event store | `AddModuleEventStore<TStore, TContext>` / `ApplyEventStoreSchemaAsync<TStore>` |`, `| Optimistic-concurrency failure | `ConcurrencyConflictException` (409 `concurrency_conflict`) |`.

- [ ] **Step 6: `GETTING_STARTED.md`, `CLAUDE.md`, `TODOS.md`, `deploy/README.md`, checklist**

- GETTING_STARTED §5 list item 3: add "open a ledger account, deposit, read `/history`" to the `.http` request list. §6 "Add a New Module": add step "5. *(Optional — only if golden rule 8 applies)* event-source an aggregate: follow AGENTS.md §5 'Add an event-sourced aggregate'." Add a new subsection:
  ```markdown
  ## 7. Removing the Ledger sample

  The Ledger module exists to show event sourcing done properly. If you don't need it: delete
  `Services/YourProject.Ledger`, `tests/YourProject.Ledger.*`, and `Shared/YourProject.ApiContracts/Ledger`;
  remove their `<Project>` entries from the `.slnx`; in the gateway remove `AddLedgerModuleServices`,
  `EnsureLedgerModuleDatabaseAsync`, the `ledgerdb` connection-string check + ancillary store, and the Ledger
  assembly in `GatewayServiceCollectionExtensions`; remove `ledgerdb` from `AppHost.cs`; drop the Ledger
  rows from `Architecture.Tests`. Keep `Shared/YourProject.EventSourcing` and the SharedKernel types — they
  are the reusable part.
  ```
- CLAUDE.md smoke-test bullet: add "The generated project includes the Ledger module and needs a `ledgerdb` connection string; `WolverineFx.Marten` is deliberately absent (ADR-0009)."
- TODOS.md: add section
  ```markdown
  ## Event sourcing (ADR-0009) — deferred

  - [ ] `dotnet new` symbol to exclude the Ledger sample (`//#if` in gateway/AppHost/slnx; doubles the smoke test).
  - [ ] Async projections / Marten daemon guidance + `AddAsyncDaemon` when an inline projection spans streams.
  - [ ] Crypto-shredding helper for PII in events (rule only today).
  - [ ] Strong-typed ids for event-sourced aggregates (Guid stream ids today).
  ```
- deploy/README.md: near the migrations note add "Event-sourced modules also apply their Marten schema at startup (`ApplyAllConfiguredChangesToDatabaseAsync`); the runtime role needs DDL rights on the module schema (already true for EF migrations)."
- `.claude/skills/allspice-clean-review/checklist.md`: after the Messaging section add
  ```markdown
  ## Event sourcing (opt-in, ADR-0009)

  - Is event sourcing **justified** for this aggregate (golden rule 8)? Flag CRUD/reference data, externally
    mirrored data, or PII-heavy aggregates being event-sourced.
  - Aggregate derives from `EventSourcedAggregate`; events are `sealed record … : IDomainEvent` with ids /
    amounts / timestamps only — **no personal data**.
  - `Apply(TEvent)` methods are pure (no validation, no throws); dispatcher is `When`, never `Apply`.
  - Bespoke `IXxxRepository : IEventSourcedRepository<Xxx>`; handlers `LoadAsync` → mutate → `SaveAsync`.
    No direct `IDocumentSession`/`LightweightSession()` in handlers.
  - Reads use projections; only an explicit history/audit query touches the stream.
  - Changed an event's shape? Requires a new stored name (`MapEventType`) + `Upcast` + tests; never an edit.
  - Marten appears only in `Infrastructure` (architecture test); `ApplyEventStoreSchemaAsync` is called in `Ensure…`.
  - Tests: Domain given/when/then; Testcontainers integration for round trip + concurrency + projection.
  ```

- [ ] **Step 7: Build docs consistency check**

Run: `grep -rn "ships only Identity + Notifications\|deliberately no sample business domain" AGENTS.md ARCHITECTURE.md README.md GETTING_STARTED.md` → no matches. `grep -n "0009" docs/adr/README.md` → one row.

- [ ] **Step 8: Commit**

```bash
git add docs/adr README.md ARCHITECTURE.md AGENTS.md GETTING_STARTED.md CLAUDE.md TODOS.md deploy/README.md .template.config/template.json .claude/skills/allspice-clean-review/checklist.md
git commit -m "docs: opt-in event sourcing — ADR-0009, golden rule 8, recipes, Ledger reference, template metadata"
```

---

### Task 14: Final verification, template smoke test, spec status

**Files:**
- Modify: `docs/superpowers/specs/2026-09-13-opt-in-event-sourcing-design.md` (status line)

- [ ] **Step 1: Full build and test**

Run: `dotnet build AllSpice.CleanModularMonolith.slnx` → 0 warnings. `dotnet test AllSpice.CleanModularMonolith.slnx --no-build` → all green (Docker running for Testcontainers).

- [ ] **Step 2: Template smoke test (from `CLAUDE.md`)**

```bash
dotnet new install .
dotnet new allspice-modular -n Acme.Demo -o ../_tmpl-smoketest
dotnet build ../_tmpl-smoketest/Acme.Demo.slnx
grep -rl "{{" ../_tmpl-smoketest --include=*.json --include=*.cs --include=*.md | grep -v "/obj/\|/bin/" || echo "no literal tokens"
ls ../_tmpl-smoketest/Services   # expect Acme.Demo.Identity, Acme.Demo.Notifications, Acme.Demo.Ledger
ls ../_tmpl-smoketest/Shared     # expect Acme.Demo.EventSourcing among others
test ! -f ../_tmpl-smoketest/CLAUDE.md && test ! -d ../_tmpl-smoketest/docs/superpowers && echo "maintainer files excluded"
dotnet new uninstall AllSpice.CleanModularMonolith
rm -rf ../_tmpl-smoketest
```
Expected: generated solution builds with 0 warnings; `Ledger`/`EventSourcing` renamed; no `{{…}}` tokens; `CLAUDE.md`/`docs/superpowers` absent; `ADR-0009` present under `docs/adr`.

- [ ] **Step 3: Update the spec status**

Change `**Status:** Approved (design); pending implementation plan` to `**Status:** Implemented (2026-09-13) — see docs/superpowers/plans/2026-09-13-opt-in-event-sourcing.md` and `**ADR:** 0009 (to be written with the implementation)` to `**ADR:** [0009](../../adr/0009-opt-in-event-sourcing-marten-enlisted.md)`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-09-13-opt-in-event-sourcing-design.md
git commit -m "docs(spec): mark opt-in event sourcing design as implemented"
```

- [ ] **Step 5: Hand off**

Do not merge or push. Report: commits on `feat/opt-in-event-sourcing`, test counts, anything that deviated from the plan (Marten API names that differed, the live-aggregation outcome from Task 6 Step 7), and open the PR only when asked (`superpowers:finishing-a-development-branch`).
