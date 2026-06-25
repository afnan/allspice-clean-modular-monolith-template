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
- [x] **F-paging — RESOLVED (2026-06-24).** `ListUsers` no longer discards `TotalCount`. The handler returns
  `Result<PagedList<UserDto>>` (page/size/total/total-pages) and the endpoint emits a `PagedResponse<UserResponse>`
  envelope. Note: Ardalis `PagedResult<T>` is deliberately **not** used as the mediator response type — it derives
  from `Result<T>` but can't be built in an error state, so `DomainExceptionResultMapper` can't map validation
  failures onto it (would turn 400s into 500s). A `DomainExceptionResultMapperTests` guard pins this. Covered by
  `ListUsersQueryHandlerTests`; new `Identity.Application.UnitTests` project hosts it.

- [x] **Unknown notification channel returns 400, not 500 — RESOLVED (2026-06-24).** Added
  `QueueNotificationRequestValidator` (FastEndpoints `Validator<QueueNotificationRequest>`) that rejects an
  unknown channel with a 400 before the endpoint resolves the SmartEnum; `QueueNotificationEndpoint` also uses
  `TryFromName` defensively. The consumer's `MapChannel` now returns `null` for an unknown channel and the handler
  logs-and-drops it as a permanent failure (consistent with its transient/permanent split) instead of throwing.
  Covered by `QueueNotificationRequestValidatorTests`.

## Phase 1 review follow-ups (deferred)

- [ ] **Dispatcher outbox atomicity not integration-tested against real Postgres.** The F3 dispatcher test
  mocks `IDbContextOutbox`; it verifies the delivered event is routed through the outbox but does not prove the
  envelope + `Delivered` status commit/rollback together. The mechanism is proven by `HybridOutboxTopologyTests`
  and the dispatcher mirrors it, but a Testcontainers test driving `DispatchPendingAsync` end-to-end (commit →
  both present; rollback → neither) would lock the dispatcher's own atomicity.
- [x] **ProblemDetails validation 400 shape unified — RESOLVED (2026-06-24).** `ErrorHandlingMiddleware` now
  emits its members (incl. `correlationId` and `errors`) at the **root** of the problem+json object (RFC7807),
  matching the root-`errors` shape of the mediator/FastEndpoints path, instead of nesting them under an
  `extensions` object. `ErrorHandlingMiddlewareTests` asserts the root shape.
- [x] **Mapper no longer 500s on non-`Result` response types — RESOLVED (2026-06-25).**
  When `TResponse` isn't an Ardalis `Result`/`Result<T>`, `DomainExceptionResultMapper` now re-throws the
  ORIGINAL exception (via `ExceptionDispatchInfo`, preserving the stack) instead of throwing
  `InvalidOperationException`. `ErrorHandlingMiddleware` then renders it with the correct status
  (`ValidationException` → 400, `NotFoundException` → 404, …) rather than a masked 500. Covered by
  `DomainExceptionResultMapperTests`.

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

- [~] **`EfRepository` track-only write silent no-op — WON'T-FIX, documented (2026-06-25).** Investigated: a
  reliable "fail loud" isn't achievable. The failure ("a write was staged but nobody flushed") is only knowable
  at scope-end, not at write time — when `AddAsync` runs, `TransactionBehavior` hasn't opened its transaction
  yet, so there's no ambient transaction to check. A dispose-time `HasChanges()` guard is fragile (pooled
  DbContexts reset their change-tracker; a deliberately rolled-back failed command false-positives), and the
  write methods all funnel through `SaveChangesAsync`, so a throwing guard is brittle against Ardalis. The real
  occurrence (the dispatcher) was fixed in Phase 0, and the track-only contract is documented on `EfRepository`.

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
- [ ] **`CurrentUserResolutionMiddleware` also resolves on proxied requests (P3, perf).** It sits in the shared
  pipeline, so authenticated YARP-proxied requests pay the `sub → local Guid` lookup even though the gateway never
  stamps audit rows for them. Acceptable at template scale; if the proxy path gets hot, scope resolution to local
  endpoints (FastEndpoints global pre-processor) or resolve lazily on first audit stamp.

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
