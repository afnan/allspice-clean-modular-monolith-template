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

## Template scaffolding

- [ ] **`{{ProjectName}}` / `{{ProjectNameLower}}` tokens are not wired (pre-existing).**
  `template.json` defines only `sourceName`; there are no `symbols` for these tokens, yet they appear in
  `*/appsettings.json`, `GatewayServiceCollectionExtensions.cs`, email templates, and `README.md`. A
  scaffolded project ships the literal `{{...}}` strings. Add `symbols` (with `replaces`) or convert to
  `sourceName`-derived values, then verify with the `dotnet new` smoke test in CLAUDE.md.

## Cross-cutting utilities (PR #4 — merged, follow-ups)

- [ ] **Audit columns store the Keycloak `sub`, not the canonical local UUID (P2).**
  `AuditableEntityInterceptor` stamps `CreatedBy`/`LastModifiedBy` from
  `HttpContextCurrentUserProvider.UserId`, which returns the JWT `NameIdentifier`/`sub` (Keycloak external
  ID). Per the project's identity convention the local UUID is canonical and the external ID is reserved for
  Keycloak/JWT boundaries. Resolve the local user GUID before stamping (or document that audit columns hold
  external IDs). Note: resolving adds a lookup per save — decide the trade-off.

- [ ] **`AddDbContextPool` bypasses Aspire's Npgsql enrichment (P2).**
  `IdentityModuleExtensions`/`NotificationsModuleExtensions` hand-roll `AddDbContextPool(UseNpgsql(...))` +
  `EnrichNpgsqlDbContext(DisableHealthChecks)`. Prefer the `AddNpgsqlDbContext<T>(name, configureDbContextOptions:
  o => o.AddInterceptors(...))` overload so Aspire's keyed connection, resilience, and telemetry wiring stays
  intact while interceptors still attach.

- [ ] **`AzureBlobStorageService`: no SAS/URL generation; cold-start container race (P3).**
  `UploadAsync` returns a raw blob name and there is no SAS/expiry story, so any "download by URL" flow against
  `PublicAccessType.None` will 403. `EnsureContainerExistsAsync` uses an unsynchronized `volatile bool` so
  concurrent first-uploads each call `CreateIfNotExistsAsync` (benign/idempotent). Add a SAS-URI generation
  method when a download-by-URL flow is needed.
