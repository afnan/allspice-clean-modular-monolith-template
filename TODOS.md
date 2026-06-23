# TODOS

Tracked follow-ups surfaced during review. Items here are deliberately deferred
because they touch runtime infrastructure that can only be verified against live
Postgres/Wolverine (Aspire or Testcontainers), not unit tests.

## Messaging / Outbox

- [ ] **Prompt outbox flush (P2).** `WolverineIntegrationEventPublisher` enrolls the module
  DbContext and persists the envelope, but nothing calls `FlushOutgoingMessagesAsync()` after
  `TransactionBehavior` commits. Persisted envelopes are delivered by Wolverine's durable
  recovery loop (seconds latency) rather than immediately. To send promptly, signal a flush
  after commit. Clean implementation needs a SharedKernel→Wolverine seam (TransactionBehavior
  lives in SharedKernel and must not depend on Wolverine), e.g. an `IOutboxFlusher` abstraction
  implemented in the gateway. Add an integration test (Testcontainers Postgres) that asserts
  commit → immediate send before changing behavior.

- [ ] **Cross-DB atomicity is a known limitation of the shared `messagingdb` model.** Envelope
  inserts (messagingdb) and command data commits (identitydb/notificationsdb) are separate
  transactions, so a crash between them can drop/orphan an event. Documented in
  `WolverineIntegrationEventPublisher` XML-doc. Revisit if true exactly-once is required
  (co-locate the outbox per module — was prototyped then reverted in favor of the single store).

## Startup / Migrations

- [ ] **Concurrent migration race (P1).** `MigrationRunner.RunWithRetryAsync` runs
  `MigrateAsync` with no cross-instance lock. Multiple gateway instances booting against the
  same database can race the same un-applied migration ("relation already exists" / partial
  apply); the retry just re-races. Serialize with a Postgres advisory lock
  (`pg_advisory_lock(<stable-hash>)` on a dedicated Npgsql connection) around the migrate, in
  the module `EnsureXModuleDatabaseAsync` extensions (Npgsql is available there; SharedKernel
  stays provider-agnostic and the SQLite test DBs are unaffected). Guard so it only runs for
  the Npgsql provider.

## In-app notification dispatch

- [ ] **Mark-dispatched-before-send loss-on-crash (P1, debated).** `NotificationDispatcher`
  marks a notification `Dispatched` and saves before `SendAsync`; a crash in between strands it
  (never re-selected, never sent). Consider a `Claimed`/`Dispatching` intermediate state with a
  reclaim timeout, or `FOR UPDATE SKIP LOCKED` claim → deliver → mark `Delivered`. (One reviewer
  considered the single-replica path acceptable; confirm desired delivery semantics first.)
