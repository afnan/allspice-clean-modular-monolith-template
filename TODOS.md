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

- [x] **Cross-DB atomicity — RESOLVED (2026-06-23, Phase 0).** Now a true transactional outbox: each
  module's envelope tables are co-located in its own database (`MapWolverineEnvelopeStorage` + enrolled
  ancillary store), so the envelope commits atomically with the state change. `messagingdb` is now the
  Wolverine main store for shared infrastructure only. Proven by `OutboxAtomicityTests` and
  `HybridOutboxTopologyTests` (Testcontainers).

## Startup / Migrations

- [x] **Concurrent migration race (P1) — RESOLVED (2026-06-23, Phase 0).** `MigrationRunner` now wraps
  `MigrateAsync` in a session-scoped `pg_advisory_lock` keyed per DbContext (held on an explicitly-opened
  connection), so concurrent instances serialize instead of racing. Npgsql-only (detected by provider name);
  SharedKernel stays provider-agnostic and SQLite test DBs migrate directly. Proven by
  `AdvisoryLockMigrationTests`.

## Phase 0 review follow-ups (deferred, not bugs in shipped behavior)

- [ ] **InviteUser Keycloak compensation no longer fires on commit-time failure (HIGH, → Phase 2 saga).**
  The UoW change means repositories stage and `TransactionBehavior` commits *after* the handler returns, so
  the handler's `try/catch` (which deletes the Keycloak user when local persistence fails) can no longer
  observe a commit-time DB failure — a failed commit orphans the Keycloak user. The window is narrow (the
  pending-invite pre-check + absence of a unique email constraint mean duplicates don't fail at commit; only
  infra faults do), but the documented compensation invariant is weaker than before. Proper fix is the
  deferred `F-compensate` saga (outbox-driven or post-commit compensation) in Phase 2; do not bolt a
  half-measure onto the clean UoW model. Tracks with the spec's open F-compensate item.

- [ ] **Outbox tests don't drive the real publisher/pipeline end-to-end (test coverage).** `OutboxAtomicity`
  and `HybridOutboxTopology` hand-roll the publish/commit sequence; they don't exercise
  `WolverineIntegrationEventPublisher` + a real `ITransactional` command through `TransactionBehavior`. The
  mechanism, co-location, and UoW are each proven separately, but an end-to-end test (real command → publisher
  → co-located outbox → delivery) would lock the wiring. Needs the Mediator source-generator-in-test setup.

- [ ] **Durability/crash-recovery path untested (test coverage).** The commit tests call
  `FlushOutgoingMessagesAsync()` for prompt in-process delivery; the "survives a crash" guarantee (envelope
  persisted, delivered by Wolverine's recovery sweep after a restart) is not exercised. Add a test that commits
  without flushing and asserts recovery-loop delivery (size the wait against the agent poll interval).

- [ ] **`EfRepository` track-only write methods fail silently for non-UoW callers (LOW, harden).** A caller
  that writes through a bespoke repository outside an `ITransactional` command (as the dispatcher did) gets a
  silent no-op. Consider making misuse loud (e.g. throw when there is no ambient transaction and the caller
  never flushes), so it fails at test time rather than in production.

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
