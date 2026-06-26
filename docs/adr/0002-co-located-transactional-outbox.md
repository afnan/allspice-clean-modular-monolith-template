# 0002 — Co-located per-module transactional outbox

- Status: Accepted
- Date: 2026-06-23

## Context

Cross-module communication uses integration events. If an event were published fire-and-forget after a
commit, a crash between the two could lose the event (state changed, no event) or emit a phantom event (event
sent, state rolled back). We need exactly-once-ish, crash-safe publication.

## Decision

Use a **true transactional outbox** with Wolverine. Each module's outbox envelope tables are **co-located in
that module's own database** (`MapWolverineEnvelopeStorage` + an enrolled ancillary store), so the envelope
commits **atomically** with the state change that produced it. A dedicated `messagingdb` is the Wolverine main
store holding only shared infrastructure (inbox, durable queues, scheduled, dead-letter).

Integration events may be published **only inside an `ITransactional` command**; publishing outside a
transaction throws by design. After commit, `TransactionBehavior` flushes the outbox immediately (best-effort)
so delivery is prompt rather than waiting for the recovery sweep.

## Consequences

- No lost or phantom integration events across a crash.
- Per-module DBs stay independently ownable; no shared-outbox coupling.
- A module that publishes events must run inside a transaction — enforced by the publisher and behavior.
- Superseded an earlier shared-`messagingdb`-only design that was not atomic with module state.
