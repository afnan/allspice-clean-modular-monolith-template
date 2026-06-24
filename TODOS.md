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

## Phase 2 notes

- [x] **F6 — temp password in the invitation pipeline — RESOLVED by removing the feature (2026-06-24).**
  The invite-user flow created a Keycloak user with an app-generated temp password — the wrong default for an
  auth-agnostic template (under SSO/SAML the IdP provisions users). The entire invite-user feature was removed
  (aggregate, command/endpoint/handler, temp-password generator, invitation-created template), so no credential
  is generated, transported, or persisted. Users are provisioned in the IdP and mirrored by the sync job.
- [x] **F-idemp — addressed by durable messaging (2026-06-24).** Envelope-level redelivery (the realistic
  duplicate) is deduped by Wolverine's durable inbox / durable local queues (Phase 0). Documented in
  `NotificationRequestedIntegrationEventConsumer`. Explicit cross-envelope dedup (processed-event store keyed on
  EventId) remains available as future hardening if needed.
- [x] **F-https — RESOLVED (2026-06-24).** `IdentityPortalOptions.RequireHttpsMetadata` (default `true`) is threaded
  onto every portal bearer scheme; the gateway sets it to `false` only in Development (`!IsDevelopment()`), so dev
  auth works against HTTP Keycloak while production still requires HTTPS metadata. Covered by `RequireHttpsMetadataTests`.
- [x] **F-paging — RESOLVED (2026-06-24).** `ListUsers` no longer discards `TotalCount`. The handler returns an
  Ardalis `PagedResult<…>` (page/size/total/total-pages) and the endpoint emits a `PagedResponse<UserResponse>`
  envelope. Covered by `ListUsersQueryHandlerTests`; new `Identity.Application.UnitTests` project hosts it.

- [ ] **Unknown notification channel 500s before the pipeline (pre-existing).** `QueueNotificationEndpoint`
  calls `NotificationChannel.FromName(req.Channel, ignoreCase: true)` before the mediator pipeline; SmartEnum
  throws `SmartEnumNotFoundException` (→ 500) for an unrecognized channel, and there's no request validator on
  `QueueNotificationRequest`. Add an endpoint validator (or `TryFromName`) returning 400. Also `MapChannel` in
  the consumer throws plain `ArgumentOutOfRangeException` for an unknown channel — inconsistent with that file's
  transient/permanent split (would dead-letter). Not a regression; surfaced during Phase 2a review.

## Phase 1 review follow-ups (deferred)

- [ ] **Dispatcher outbox atomicity not integration-tested against real Postgres.** The F3 dispatcher test
  mocks `IDbContextOutbox`; it verifies the delivered event is routed through the outbox but does not prove the
  envelope + `Delivered` status commit/rollback together. The mechanism is proven by `HybridOutboxTopologyTests`
  and the dispatcher mirrors it, but a Testcontainers test driving `DispatchPendingAsync` end-to-end (commit →
  both present; rollback → neither) would lock the dispatcher's own atomicity.
- [ ] **Two ProblemDetails shapes for validation 400s.** The Mediator pipeline path returns Ardalis
  `Result.Invalid` (root `errors` via FastEndpoints); the `ErrorHandlingMiddleware` fallback nests under
  `extensions.errors` (non-RFC7807 root). Unify if a single client-facing error contract is desired.
- [ ] **Mapper 500s for `DomainException`/`ValidationException` on non-`Result` response types.**
  `DomainExceptionResultMapper` only maps to `Result`/`Result<T>`; a handler whose response isn't an Ardalis
  Result that throws a domain/validation exception yields `InvalidOperationException` → 500. Pre-existing; the F2
  reorder slightly widens exposure. Consider a graceful fallback.

## Startup / Migrations

- [x] **Concurrent migration race (P1) — RESOLVED (2026-06-23, Phase 0).** `MigrationRunner` now wraps
  `MigrateAsync` in a session-scoped `pg_advisory_lock` keyed per DbContext (held on an explicitly-opened
  connection), so concurrent instances serialize instead of racing. Npgsql-only (detected by provider name);
  SharedKernel stays provider-agnostic and SQLite test DBs migrate directly. Proven by
  `AdvisoryLockMigrationTests`.

## Phase 0 review follow-ups (deferred, not bugs in shipped behavior)

- [x] **InviteUser Keycloak compensation — MOOT, feature removed (2026-06-24).** F-compensate only existed to
  roll back the Keycloak user that `InviteUserCommandHandler` created. The invite-user feature was removed
  entirely (the app no longer creates Keycloak users), so there is no external side-effect to compensate. No
  saga needed.

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

- [x] **`{{ProjectName}}` / `{{ProjectNameLower}}` tokens wired — RESOLVED (2026-06-24, F-tokens).**
  `template.json` now declares `symbols`: a `derived` symbol off `name` (identity form) replaces `{{ProjectName}}`,
  and a lower-cased form replaces `{{ProjectNameLower}}` (Keycloak realm, Redis instance name, cache-key prefix).
  The `dotnet new` smoke test confirms a scaffolded project (`Acme.Demo`) ships no literal `{{...}}` and builds clean.
  `TODOS.md` and `docs/superpowers/**` are now excluded from generated projects (maintainer planning artifacts).

## Cross-cutting utilities (PR #4 — merged, follow-ups)

- [x] **Audit columns now store the canonical local UUID — RESOLVED (2026-06-24, F-identity).**
  `IUserExternalIdResolver` gained `GetLocalIdByExternalIdAsync` (the cross-module `sub → local Guid` seam).
  `CurrentUserResolutionMiddleware` resolves the subject to the local UUID **once per authenticated request**
  and caches it in the scoped `ICurrentUserContext`; `HttpContextCurrentUserProvider.UserId` returns that local
  UUID (never the external `sub`), and the audit interceptor stamps it. Unauthenticated/unsynced users yield
  unattributed stamps (null) rather than an external id. Cost: one directory lookup per authenticated request
  (the user is fixed for the request). Future optimisation: a short-TTL `sub → Guid` cache across requests.

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
