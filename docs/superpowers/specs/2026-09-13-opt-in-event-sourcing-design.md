# Opt-in Event Sourcing — Design

**Date:** 2026-09-13
**Status:** Implemented (2026-09-14) — see docs/superpowers/plans/2026-09-13-opt-in-event-sourcing.md
**ADR:** [0009](../../adr/0009-opt-in-event-sourcing-marten-enlisted.md)
**Scope:** Add event sourcing to the template as a **per-aggregate opt-in** inside a normal module. The default
persistence model (EF Core aggregates + bespoke repositories + `TransactionBehavior`) is unchanged and remains
the recommended path for most aggregates.

---

## 1. Problem & decisions

Most applications have a *few* aggregates where the history is the business record — ledgers, payments,
approvals, anything that must answer "what did it look like on date X" or survive an audit. Today the template
offers only state-based EF Core persistence, so teams either bolt on an ad-hoc audit table or event-source
everything. Both are wrong. This design adds a first-class, guard-railed way to event-source **the aggregates
that fit**, in the same module and the same transaction as ordinary EF aggregates.

| Decision | Choice |
| --- | --- |
| **Granularity** | Opt-in **per aggregate**, not per module. A module may mix EF aggregates and event-sourced aggregates. |
| **Event store** | **Marten** (JasperFx sibling of Wolverine), Postgres-native, one store per module in that module's own database (schema `<module>`). |
| **Transaction owner** | The module `DbContext` — as today. Marten sessions **enlist** in that transaction (`SessionOptions.ForTransaction(tx, shouldAutoCommit: false)`); `TransactionBehavior` alone commits/rolls back. The existing EF-hosted Wolverine outbox is untouched. |
| **Why not `WolverineFx.Marten`** | `IntegrateWithWolverine()` makes Marten a *second* unit of work and outbox; handlers can target only one ancillary store, and mixing EF and Marten ancillary stores in one host is undocumented. That forces "per module" event sourcing — the opposite of the goal. |
| **Read side** | Marten **inline** single-stream projections (committed in the same transaction). Ordinary EF read models remain available for anything else. |
| **Reference implementation** | New **Ledger** module with one event-sourced `Account` aggregate — a textbook fit. Ships always, documented as deletable. |
| **Guard rails** | Golden rule 8 (fit criteria), architecture-fitness tests (Marten only in Infrastructure), review checklist items, ADR 0009. |

### Key properties

- **Atomic.** Event append, inline projection update, ordinary EF writes, and the outbox envelope commit in
  **one** Postgres transaction; a failure anywhere rolls back everything.
- **Optimistic concurrency.** `FetchForWriting<T>` carries the stream's expected version; a concurrent writer
  surfaces as a `409 concurrency_conflict`.
- **Domain purity preserved.** Domain layers never reference Marten. Stream events *are* the aggregate's domain
  events, so same-module `IDomainEventHandler<T>` and `IIntegrationEventPublisher` work unchanged.
- **Production-grade, not demo.** Event versioning (upcasting), projection rebuild, idempotency/correlation
  metadata, stream archiving and a PII rule are part of the design.

---

## 2. Dependencies

| Package | Version | Note |
| --- | --- | --- |
| `Marten` | 9.33.0 | net10 target. Depends on `JasperFx >= 2.67`, `Weasel.Postgresql 9.31.1` (`Npgsql >= 9.0.4`; repo resolves 10.0.3 — compatible). |
| `WolverineFx`, `WolverineFx.RuntimeCompilation`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql` | 6.36.0 (all four aligned) | Required: Wolverine 6.16 pins `JasperFx 2.13`, which cannot coexist with Marten 9.33's `JasperFx >= 2.67`. Currently split 6.16 / 6.14. |

`WolverineFx.Marten` is **not** added (see §1). Versions live in `Directory.Packages.props` only.

---

## 3. Placement

```
Shared/AllSpice.CleanModularMonolith.SharedKernel/
  Persistence/ITransactionParticipant.cs            -- NEW: joins the module DbContext transaction
  EventSourcing/IEventSourcedAggregate.cs           -- NEW: store-facing surface (version, uncommitted, archive)
  EventSourcing/EventSourcedAggregate.cs            -- NEW: base type (no Marten)
  EventSourcing/IEventSourcedRepository.cs          -- NEW: Load/Add/Save/History contract
  Exceptions/ConcurrencyConflictException.cs        -- NEW: 409 concurrency_conflict
  Behaviors/TransactionBehavior.cs                  -- CHANGED: participants, lazy/early tx, discard

Shared/AllSpice.CleanModularMonolith.EventSourcing/ -- NEW project (references Marten + SharedKernel)
  ModuleEventStoreExtensions.cs                     -- AddModuleEventStore<TStore,TContext>(...)
  MartenTransactionParticipant.cs                   -- scoped; opens session inside the module tx
  MartenEventSourcedRepository.cs                   -- base for bespoke repositories
  IEventMetadataProvider.cs / HttpEventMetadataProvider.cs (gateway)  -- correlation + idempotency headers
  IModuleEventStoreSession.cs                       -- what repositories need from the participant
  (Marten ConcurrencyException -> ConcurrencyConflictException translation lives inside the participant's FlushAsync)

Services/AllSpice.CleanModularMonolith.Ledger/       -- NEW reference module (see §6)

AllSpice.CleanModularMonolith.ApiGateway/
  Program.cs                                        -- CHANGED: RunJasperFxCommands (projection rebuild CLI)
  Extensions/GatewayModuleRegistrationExtensions.cs -- CHANGED: Ledger registration + ancillary store
  Extensions/GatewayServiceCollectionExtensions.cs  -- CHANGED: Ledger API assembly
  Middleware/ErrorHandlingMiddleware.cs             -- CHANGED: ConcurrencyConflictException -> 409

AllSpice.CleanModularMonolith.AppHost/AppHost.cs    -- CHANGED: ledgerdb resource
```

Marten may be referenced **only** by the `EventSourcing` shared project and by `*.Infrastructure*` namespaces of
modules. This is enforced by an architecture test (§8).

---

## 4. Transaction lifecycle

### 4.1 Rule

> The module transaction is opened **lazily**: by `TransactionBehavior` at commit time (today's path), or
> **earlier** by an event-store session on first write-intent inside the handler. In both cases
> `TransactionBehavior` is the only component that commits or rolls back.

Marten's write API (`FetchForWriting<T>` → `AppendMany` → `SaveChangesAsync`) needs a live session during the
handler. That session must run inside the module transaction to be atomic with the outbox, so the participant
begins the transaction on the owner `DbContext` when the handler first asks for a session. EF repositories keep
staging as today; nothing about their behaviour changes.

### 4.2 `ITransactionParticipant` (SharedKernel)

```csharp
public interface ITransactionParticipant
{
    DbContext Owner { get; }                       // the module DbContext whose transaction it joins
    bool HasPendingChanges { get; }                // staged appends/archives not yet flushed
    ValueTask FlushAsync(CancellationToken ct);    // session.SaveChangesAsync inside Owner's current tx
    IEnumerable<IDomainEvent> TakeDomainEvents();  // from aggregates loaded/added through it
    void Discard();                                // drop pending work (failure Result / rollback)
}
```

Registered **scoped**, one per (module store, module context).

### 4.3 `TransactionBehavior` changes (exactly four)

1. **Dirty set** = contexts with `ChangeTracker.HasChanges()` **∪** `Owner` of every participant with
   `HasPendingChanges`. The "exactly one module per command" guard runs on this set (a participant's owner
   counts as that module).
2. **Transaction reuse.** If `db.Database.CurrentTransaction` is non-null (opened early by a participant), it is
   used; otherwise `BeginTransactionAsync` as today.
3. **Drain loop.** Each iteration: `db.SaveChangesAsync` → `participant.FlushAsync` for participants of `db` →
   collect domain events from the change tracker **and** `participant.TakeDomainEvents()` → dispatch → repeat
   until no events. Inline projections run during `FlushAsync`, inside the transaction.
4. **Failure paths.** The failure-Result path and the `catch` path call `participant.Discard()` for every
   participant, and roll back an early-opened transaction (today's failure-Result path only clears the
   tracker because no transaction exists yet; with an early transaction it must roll back).

Post-commit behaviour (outbox flush, `IPostCommitActions`) is unchanged.

### 4.4 `WolverineIntegrationEventPublisher`

Unchanged. It already locates the context with an active transaction; an early-opened transaction satisfies
its guard, so a domain-event handler for a stream event can publish an integration event in the same
transaction.

---

## 5. Domain & infrastructure surface

### 5.1 `EventSourcedAggregate` (SharedKernel, no Marten)

Stream ids are **`Guid`** (`Entity<Guid>`). Strong-typed ids for event-sourced aggregates are deferred (TODOS):
Marten's live aggregation with strong-typed identifiers could not be verified against the docs, and a Guid
keeps the reference implementation free of that risk.

```csharp
public abstract class EventSourcedAggregate : Entity<Guid>, IAggregateRoot, IEventSourcedAggregate
{
    public long Version { get; private set; }                 // stream version when loaded; set by the store
    public IReadOnlyList<IDomainEvent> UncommittedEvents { get; }
    public bool IsMarkedForArchive { get; private set; }

    protected void Raise(IDomainEvent @event);                 // Apply(@event) + record + RegisterDomainEvent
    protected void MarkForArchive();                           // repository archives the stream at Save

    // IEventSourcedAggregate (explicit, store-facing): SetVersion(long), ClearUncommittedEvents()
}
```

- **State transitions** are `public void Apply(TEvent e)` methods — Marten's aggregation convention, but only a
  naming convention, so Domain stays Marten-free. `Apply` methods contain no validation.
- **Command methods** guard invariants (`Ardalis.GuardClauses`, `DomainException` subtypes) then `Raise(...)`.
  `Raise` calls `protected abstract void When(IDomainEvent @event)`, a `switch` that forwards to the matching
  `Apply(TEvent)`. The dispatcher must **not** be named `Apply`: Marten's convention scanner also accepts
  interface-typed `Apply` overloads, so an `Apply(IDomainEvent)` would make Marten apply every event twice.
- **Constructor:** a non-public parameterless constructor (Marten builds aggregates through it — documented as
  not needing to be public) plus static factories for the first event.
- **Stream events** are `public sealed record Xxx(...) : IDomainEvent` in `Domain/Events`, carrying business
  time (`DateTimeOffset OccurredOnUtc`) and ids only. They are the domain events — no translation layer.
- **Version** is set by the repository from `IEventStream<T>.CurrentVersion` after `FetchForWriting`, not by
  a Marten property convention, so it does not depend on undocumented behaviour.

### 5.2 `IEventSourcedRepository<TAggregate>` (SharedKernel)

```csharp
public interface IEventSourcedRepository<TAggregate> where TAggregate : EventSourcedAggregate
{
    Task<TAggregate?> LoadAsync(Guid id, CancellationToken ct);         // FetchForWriting (write-intent)
    Task AddAsync(TAggregate aggregate, CancellationToken ct);          // StartStream
    Task SaveAsync(TAggregate aggregate, CancellationToken ct);         // AppendMany (+ ArchiveStream)
    Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid id, CancellationToken ct); // stream + metadata
}
```

Golden rule 4 holds: each aggregate gets a bespoke `IXxxRepository : IEventSourcedRepository<Xxx>`
(plus any projection queries) and `XxxRepository : MartenEventSourcedRepository<Xxx, IXxxEventStore>`. Handlers
depend on the bespoke interface.

`StoredEvent` (SharedKernel) = `(long Version, long Sequence, DateTimeOffset Timestamp, string EventType,
string? CorrelationId, IReadOnlyDictionary<string, object?> Headers, IDomainEvent Data)`.

### 5.3 `MartenEventSourcedRepository<TAggregate, TStore>` (EventSourcing project)

- `LoadAsync`: `session = await participant.GetSessionAsync(ct)`; `stream = await session.Events
  .FetchForWriting<TAggregate>(id.Value, ct)`; returns `null` when `stream.Aggregate is null`; otherwise sets
  `Version = stream.CurrentVersion`, registers the aggregate with the participant (for domain-event
  collection) and remembers the `IEventStream<TAggregate>` for `SaveAsync`.
- `AddAsync`: `session.Events.StartStream<TAggregate>(id.Value, uncommitted)`; registers with participant.
- `SaveAsync`: `stream.AppendMany(uncommitted)`; if `IsMarkedForArchive` → `session.Events.ArchiveStream(id)`.
  Uncommitted events are cleared **after** a successful `FlushAsync` (participant callback), not at `SaveAsync`.
- `HistoryAsync`: `session.Events.FetchStreamAsync(id)` on a **query session** (no transaction), mapped to
  `StoredEvent`.
- Reads of projections use `store.QuerySession()`; they never open a transaction.
- Unregistered aggregate types fall back to Marten **live aggregation** (documented), so `FetchForWriting`
  works without registering the write model as a projection. Marten's `ConcurrencyException` (thrown at
  `SaveChangesAsync`, i.e. inside `FlushAsync`) is translated to `ConcurrencyConflictException`.

### 5.4 `MartenTransactionParticipant<TStore, TContext>`

- `GetSessionAsync(ct)`: if no session yet — ensure `Owner.Database.CurrentTransaction` exists (begin one if
  not; connection opened by EF), then `store.LightweightSession(SessionOptions.ForTransaction(npgsqlTx,
  shouldAutoCommit: false))`; set `session.CorrelationId` and `SetHeader("idempotency-key", …)` from
  `IEventMetadataProvider`. Returns the same session for the rest of the scope.
- `HasPendingChanges`: any registered aggregate has uncommitted events or is marked for archive.
- `FlushAsync`: `session.SaveChangesAsync(ct)` (translating exceptions), then clears uncommitted events on the
  registered aggregates.
- `TakeDomainEvents`: drains `TakeDomainEvents()` from registered aggregates.
- `Discard`: disposes the session, clears registrations. Disposal is also guaranteed at scope end.

### 5.5 `AddModuleEventStore<TStore, TContext>(builder, connectionString, schemaName, configure)`

Registers `services.AddMartenStore<TStore>(opts => …)` with these fixed settings, then the module's
`configure(opts)` (projections, upcasters, event type mappings):

| Setting | Value | Reason |
| --- | --- | --- |
| `DatabaseSchemaName` | `schemaName` (module name, e.g. `ledger`) | Isolation inside the module DB; outbox stays in `wolverine` schema |
| `AutoCreateSchemaObjects` | `AutoCreate.None` | Schema applied explicitly at startup (below), like EF migrations |
| Serializer | System.Text.Json | Matches the rest of the stack |
| `Events.AppendMode` | `EventAppendMode.Rich` | Marten 9 defaults to `QuickWithServerTimestamps`, which withholds versions from inline projections and forbids expected-version appends |
| `Events.MetadataConfig.CorrelationIdEnabled` / `HeadersEnabled` | `true` | Correlation + idempotency on every event |
| `Events.StreamIdentity` | `Guid` | All template ids are Guid-backed |

Also registers `ITransactionParticipant` → `MartenTransactionParticipant<TStore, TContext>` (scoped) and the
`TStore`-typed participant for repositories. Nothing else in the host changes: a module with no event-sourced
aggregates never calls this and never loads Marten.

### 5.6 Schema management

`Ensure{Module}ModuleDatabaseAsync` runs EF migrations via `MigrationRunner` (as today) **then**
`await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync()`. Marten guards this with its own advisory
lock, so concurrent replicas are safe. `deploy/README.md` notes that the runtime role needs DDL rights on the
module schema (already true for EF migrations).

### 5.7 Projection rebuild (maintenance CLI)

`Program.cs` adds `builder.Host.ApplyJasperFxExtensions()` and ends with `return await
app.RunJasperFxCommands(args)` (a no-arg `dotnet run` still starts the host normally). Rebuild:

```bash
dotnet run --project AllSpice.CleanModularMonolith.ApiGateway -- projections rebuild
dotnet run --project AllSpice.CleanModularMonolith.ApiGateway -- projections -p AccountSummary rebuild
```

This also makes Wolverine's `codegen write` genuinely available (ARCHITECTURE.md already claims it).

### 5.8 Event versioning (upcasting)

Convention (documented in AGENTS.md, demonstrated in Ledger):

1. Keep the **old CLR type** for deserialization, renamed with a version suffix (`FundsDepositedV1`, a plain
   record — not an `IDomainEvent`, never raised).
2. The **new** shape keeps the canonical CLR name (`FundsDeposited`) and gets a **new stored name**:
   `opts.Events.MapEventType<FundsDeposited>("funds_deposited_v2")` — avoiding a collision with the old
   stored name (Marten's default snake_case mapping would otherwise reuse `funds_deposited`).
3. Register `opts.Events.Upcast<FundsDepositedV1, FundsDeposited>("funds_deposited", FundsDepositedUpcasts.FromV1)`;
   the stored-name argument binds the old rows to `FundsDepositedV1`, and the pure `FromV1` function (Domain)
   produces the current shape. `Apply` methods exist only for the current shape.
4. Never edit or delete stored events.

### 5.9 Idempotency, correlation, PII

- `IEventMetadataProvider` (EventSourcing project; gateway implementation reads `CorrelationIdMiddleware`'s id
  and the `Idempotency-Key` header). Both are stamped on every appended event.
- Events carry **no personal data** — ids, amounts, timestamps, references only. Crypto-shredding is named in
  AGENTS.md as the escape hatch if PII in events ever becomes unavoidable; it is not implemented.

### 5.10 Errors

`ConcurrencyConflictException : DomainException` — message "…was modified by another request; reload and
retry", code `concurrency_conflict`. Mapped to 409 in `ErrorHandlingMiddleware` and in the Result→HTTP mapping.
EF's `DbUpdateConcurrencyException` is translated to the same exception in `TransactionBehavior`'s catch (a
small consistency gain: both persistence styles surface identical conflicts).

---

## 6. Ledger module (reference implementation)

**Purpose:** a small, complete, *correct* example of an aggregate that genuinely fits event sourcing, using
every part of §4–§5 so a developer can copy the shape. It is not a product feature.

### 6.1 Domain

- `Account : EventSourcedAggregate` (Guid id, see §5.1).
  - `static Account Open(Guid accountId, Guid ownerUserId, Currency currency, DateTimeOffset nowUtc)`
  - `void Deposit(Money amount, string reference, DateTimeOffset nowUtc)` — positive amount, same currency,
    account open.
  - `void Withdraw(Money amount, string reference, DateTimeOffset nowUtc)` — as above **and** sufficient
    balance, else `BusinessRuleViolationException` (`insufficient_funds`).
  - `void Close(DateTimeOffset nowUtc)` — zero balance required; calls `MarkForArchive()`.
- Events (`Domain/Events`, sealed records implementing `IDomainEvent`): `AccountOpened`, `FundsDeposited`,
  `FundsWithdrawn`, `AccountClosed`; plus `FundsDepositedV1` (legacy shape without `Reference`) with its
  upcaster — the worked versioning example (§5.8).
- Value objects: reuse/introduce `Money` (+ `Currency` SmartEnum) per the existing DDD checklist.

### 6.2 Application

- Commands (`ITransactional`): `OpenAccount`, `DepositFunds`, `WithdrawFunds`, `CloseAccount` — each loads via
  `IAccountRepository`, calls the aggregate, `SaveAsync`, returns `Result`/`Result<T>`.
- Queries: `GetAccountSummary` (projection document), `GetAccountHistory` (stream via `HistoryAsync`).
- `IDomainEventHandler<AccountOpened>` publishes the **existing** `NotificationRequestedIntegrationEvent`
  (Notifications.Contracts) via `IIntegrationEventPublisher` — proves event-sourced write + outbox atomicity
  without a new Contracts project.
- Validators per command/query as elsewhere.

### 6.3 Infrastructure

- `ILedgerEventStore : IDocumentStore` marker; `LedgerDbContext : DbContext, IModuleDbContext` with **only** the
  Wolverine envelope mapping (it owns the transaction and hosts the co-located outbox; a real module would add
  its EF aggregates here).
- `AccountSummary` inline single-stream projection document: `Id, OwnerUserId, Currency, Balance, Status,
  Version, LastActivityUtc` — registered `ProjectionLifecycle.Inline`.
- `AccountRepository : MartenEventSourcedRepository<Account, AccountId, ILedgerEventStore, LedgerDbContext>,
  IAccountRepository` adding `GetSummaryAsync(id)`.
- `LedgerModuleExtensions.AddLedgerModuleServices` (DbContext with Wolverine integration, `AddModuleEventStore`,
  repositories, Mediator, validators, permission manifest, health check) and `EnsureLedgerModuleDatabaseAsync`
  (§5.6). `LedgerPermissionManifest`: `ledger.access`, `ledger:accounts.read`, `ledger:accounts.write`.
- Gateway: ancillary store `PersistMessagesWithPostgresql(ledgerConnectionString, "wolverine",
  Ancillary).Enroll<LedgerDbContext>()`; connection-string guard extended; API assembly registered. AppHost:
  `postgres.AddDatabase("ledgerdb")` + `.WithReference(ledgerDatabase)` on the gateway.

### 6.4 API

Routes carry the template's existing `/api` prefix (cf. `/api/notifications`, `/api/identity/...`).

| Method | Route | Permission |
| --- | --- | --- |
| POST | `/api/ledger/accounts` | `ledger:accounts.write` |
| POST | `/api/ledger/accounts/{id}/deposits` | `ledger:accounts.write` |
| POST | `/api/ledger/accounts/{id}/withdrawals` | `ledger:accounts.write` |
| POST | `/api/ledger/accounts/{id}/close` | `ledger:accounts.write` |
| GET | `/api/ledger/accounts/{id}` | `ledger:accounts.read` |
| GET | `/api/ledger/accounts/{id}/history` | `ledger:accounts.read` |

Response DTOs in `ApiContracts/Ledger`. Concurrency conflicts → 409 `concurrency_conflict`; insufficient
funds → 422 `insufficient_funds`.

### 6.5 Removing the sample

Documented in README/GETTING_STARTED: delete `Services/*.Ledger`, its three test projects and
`ApiContracts/Ledger`; remove the `.slnx` entries, the three gateway registration lines, the `Program.cs`
`Ensure` call, the AppHost database, and `ledgerdb` from the connection-string guard. The `EventSourcing`
project and SharedKernel types stay (they are the reusable part). A `dotnet new` switch to exclude the sample is
deferred (TODOS.md).

---

## 7. Documentation & template metadata

| File | Change |
| --- | --- |
| `.template.config/template.json` | `classifications` += `"Event Sourcing"`; `description` mentions opt-in event sourcing. |
| `README.md` | Tagline: "…Clean Architecture, CQRS, event-driven messaging, and **opt-in event sourcing (Marten)**". Feature bullet. Project Layout rows (`Services/*.Ledger`, `Shared/*.EventSourcing`). Modules table row **Ledger**. "Removing the Ledger sample" note. |
| `ARCHITECTURE.md` | Key-libraries row *Event store · Marten (per-module store, enlisted in the module EF transaction)*. New `## Event sourcing (opt-in per aggregate)`: lifecycle (§4), base types, participant, projections, schema, rebuild, versioning, archive, PII. New `## Ledger module (reference)`. Database-strategy paragraph (Marten schema in the module DB; `ApplyAllConfiguredChangesToDatabaseAsync` at startup). Testing note (Testcontainers for Marten). Correct the `codegen write` claim now that the JasperFx runner exists. |
| `AGENTS.md` | **Golden rule 8 — Event sourcing is opt-in per aggregate and must be justified.** Fit: history/audit is a first-class requirement; temporal ("as of") queries; financial/ledger-style append flows; several derived read models. Not a fit: CRUD/reference data; data mirrored from external systems (Keycloak users); aggregates with PII churn; anything where only current state matters. §1 flow line mentions the event-sourced path. §3 DO patterns for the ES layer (base type, `Apply` purity, `Raise`, bespoke repository, inline projection, upcaster convention). §4 DON'Ts: no PII in events; never mutate/delete events; don't list from the stream — use projections; don't event-source everything; don't open Marten sessions outside the participant. §5 recipes: *Add an event-sourced aggregate*, *Evolve an event (upcaster)*, *Rebuild a projection*; replace "ships only Identity + Notifications" with the Ledger reference statement. §6 utilities rows (`EventSourcedAggregate`, `IEventSourcedRepository`, `AddModuleEventStore`, `ConcurrencyConflictException`). |
| `GETTING_STARTED.md` | "Add a New Module": optional event-store step. "Verify": Ledger endpoints smoke. "Removing the Ledger sample". |
| `docs/adr/0009-opt-in-event-sourcing-marten-enlisted.md` + `docs/adr/README.md` | Decision, alternatives (per-module `WolverineFx.Marten`; hand-rolled event table), consequences. |
| `.claude/skills/allspice-clean-review/checklist.md` | New "Event sourcing" section: fit justified, no PII, `Apply` has no validation, events sealed + `IDomainEvent`, versioning via mapped names + upcaster, projections for lists, 409 on conflict, tests (given-when-then + Postgres integration). |
| `deploy/README.md` | DDL rights for the startup Marten schema apply. |
| `CLAUDE.md` | Smoke-test note: generated project needs `ledgerdb`; `WolverineFx.Marten` deliberately absent. |
| `TODOS.md` | Deferred: `dotnet new` switch to exclude the Ledger sample; async projections / daemon; crypto-shredding helper. |

---

## 8. Testing

| Project | Tests |
| --- | --- |
| `SharedKernel.UnitTests` | `TransactionBehavior` with a fake participant: pending-only participant opens/commits a tx; early-opened tx is reused not re-opened; failure Result → `Discard` + rollback; exception → `Discard` + rollback; `FlushAsync` runs each drain iteration; participant domain events are dispatched; participant owner counted in the one-module guard (two modules → throws). `EventSourcedAggregate`: `Raise` applies + records + registers; `SetVersion`/`ClearUncommittedEvents`; `MarkForArchive`. |
| `Ledger.Domain.UnitTests` (new) | Given-events/When/Then for every command; invariants (`insufficient_funds`, closed account, currency mismatch, non-zero close); replay determinism; `FundsDepositedV1 → FundsDeposited` upcaster. |
| `Ledger.Application.UnitTests` (new) | Handlers with mocked `IAccountRepository`; `AccountOpened` handler publishes `NotificationRequestedIntegrationEvent`. |
| `EventSourcing.IntegrationTests` (new, Testcontainers Postgres) | The de-risking gate, with a probe aggregate and **before** Ledger exists: add/load round trip + version; unknown stream → null; rollback discards events and EF rows together; concurrent writer → `ConcurrencyConflictException`; domain events taken from tracked aggregates and cleared after flush; `HistoryAsync` metadata; archive on retire. |
| `Ledger.Infrastructure.IntegrationTests` (new, Testcontainers Postgres) | Inline projection equals stream state (balance, status, version); unknown summary → null; close archives + summary reads Closed; legacy `funds_deposited` row upcasts on load and in history; current deposits stored as `funds_deposited_v2`; `ApplyAllConfiguredChangesToDatabaseAsync` idempotent. |
| `Foundation.IntegrationTests` | Extend `OutboxAtomicityTests`/`TwoModuleHost` with an event-sourced probe: (a) append + EF row + envelope commit together; (b) handler failure **after** append → no events, no projection document, no envelope, no row. |
| `Architecture.Tests` | Marten referenced only from `*.Infrastructure*` namespaces and the `EventSourcing` project; Ledger does not depend on Identity/Notifications internals (Contracts allowed) and vice-versa; existing rules (sealed domain events, aggregates in Domain, handlers in Application) applied to Ledger. |

Definition of Done is unchanged: `dotnet build` with 0 warnings, `dotnet test` green, plus the template smoke
test in `CLAUDE.md` (generated project builds; `ledgerdb` present; no `{{…}}` tokens).

---

## 9. Out of scope / deferred

- Async projections and the Marten daemon (documented as the upgrade path when an inline projection becomes
  too expensive or spans streams).
- Crypto-shredding for PII in events (rule only).
- A `dotnet new` switch to exclude the Ledger sample.
- Snapshotting of the write model beyond Marten live aggregation (register the aggregate as an inline
  snapshot if streams grow long; documented, not shipped).
- Strong-typed ids for event-sourced aggregates (Guid stream ids ship; see §5.1).
- `WolverineFx.Marten` / Marten-hosted outbox.

---

## 10. Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Wolverine 6.16 → 6.36 bump surfaces breaking changes | Bump first as its own commit; full build + test before any Marten code. |
| Holding the module transaction open during the handler (early open) lengthens lock windows | Only event-sourced commands open early; handlers are in-memory work + reads. Documented. |
| Marten `Version`-property convention differs from expectation | Not relied upon — version comes from `IEventStream<T>.CurrentVersion`. |
| `FetchForWriting` on an unregistered aggregate | Documented live-aggregation fallback; covered by integration tests. |
| Developers event-source everything | Golden rule 8 + review checklist + ADR; Ledger shows the *shape* of a fit. |
