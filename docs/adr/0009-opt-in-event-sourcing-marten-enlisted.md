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
- Marten 9 needs `JasperFx ≥ 2.67`, which required aligning the Wolverine family from 6.16 to 6.36.
- Projection changes need `dotnet run -- projections rebuild` (JasperFx command runner now in `Program.cs`).
- **Fail-fast schema guard.** `AddModuleEventStore`'s `configure` callback must register at least one event
  type or projection (typically `opts.Projections.LiveStreamAggregation<TAggregate>()` per event-sourced
  aggregate) — otherwise Marten's Events schema feature never activates and no `mt_events`/`mt_streams`
  tables would be created. `ApplyEventStoreSchemaAsync<TStore>` checks this and throws
  `InvalidOperationException` at startup ("has no event types or projections registered…") instead of
  surfacing a "relation does not exist" error much later at runtime.
- **Constructor-skipping rule.** Marten's live aggregation (`FetchForWriting`/`LiveStreamAggregation`)
  rebuilds an aggregate by allocating it without running any constructor, then replays history straight
  through `Apply(TEvent)`. No state on an event-sourced aggregate may depend on a field/property initializer
  or constructor logic — every property must be assigned by an `Apply` method, and collections must be
  lazily initialized (`EventSourcedAggregate` and `HasDomainEventsBase` already do this for their
  uncommitted/domain-event lists).
- An empty EF migration for a module with no EF entities (only the event store) is expected: the outbox
  envelope tables are `ExcludeFromMigrations` and provisioned separately by Wolverine's `Admin.MigrateAsync`.
- Deferred: async projections/daemon, crypto-shredding helper, a `dotnet new` switch to omit the Ledger sample.
