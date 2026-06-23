# Design: Correctness Overhaul + Ideal Transactional Outbox

**Date:** 2026-06-23
**Status:** Approved (design); pending implementation plan
**Scope:** Fix *all* findings from the 2026-06-23 code review, built on top of two architectural corrections: a real Unit of Work and a per-module (co-located) transactional outbox.
**Audience note:** This is a learning codebase. The spec deliberately explains *why* each change is correct, not only *what* to change. Where a fix has a "textbook" form, we take it even when a smaller patch would compile.

---

## 1. Goals & non-goals

### Goals
- Make the template's documented guarantees **actually true**: `ITransactional` commands are atomic; integration events are atomic with the state change that produced them; validation failures return `400`.
- Fix every correctness, security, DRY, and consistency finding from the review.
- Leave the codebase in a state where the docs (`AGENTS.md`, `ARCHITECTURE.md`, code comments) match the code.
- Every change lands behind a test that **fails before and passes after** (Definition of Done in `AGENTS.md` §2).

### Non-goals
- No new features. No new modules.
- No change to the public HTTP contract except status codes that are currently *wrong* (validation `500 → 400`).
- No reformatting/refactor unrelated to a finding. (The primary-constructor sweep is in scope because it was an explicit review finding and a stated consistency goal.)

---

## 2. The two architectural corrections (the foundation)

Everything else sits on these. They are sequenced first because later fixes (the outbox, the dispatcher publish) depend on them being correct.

### 2.1 Correction A — a real Unit of Work

**The bug (review finding #1).** `TransactionBehavior` opens its transaction on the *first* `IModuleDbContext` in DI order (`TransactionBehavior.cs:45-56`). That selection runs *before* the handler, when no context has a transaction yet, so it always picks the first-registered module — `Notifications` (`GatewayModuleRegistrationExtensions.cs:38-39`). Meanwhile the bespoke repositories extend Ardalis `RepositoryBase<T>` directly, whose `AddAsync`/`UpdateAsync`/`DeleteAsync` **call `SaveChanges` immediately**. So for an Identity command:

1. Transaction opens on `notificationsdb` (wrong, and unused).
2. `_userRepository.AddAsync(user)` saves to `identitydb` — which has *no* transaction — and commits on the spot.
3. `_invitationRepository.AddAsync(invitation)` commits separately.
4. `TransactionBehavior` commits an empty transaction on `notificationsdb`.

Result: the user and invitation rows are two independent writes with no transaction around them. A failure between them orphans a row; the handler's compensation logic assumes a rollback that never happens. The bug is invisible in tests because the test suite registers one module at a time, so "the first context" is always the right one.

**The fix (chosen approach: defer SaveChanges to the behavior — true UoW).**

- Introduce/repoint repositories so write methods (`Add`/`Update`/`Delete`) only **stage** entities in the change tracker. They no longer flush.
  - Re-point the bespoke repos (`UserRepository`, `NotificationRepository`, `InvitationRepository`, `NotificationPreferenceRepository`, `NotificationTemplateRepository`) at the shared `EfRepository<TContext, TAggregate>` base (today they extend Ardalis `RepositoryBase<T>` directly and bypass `EfRepository` entirely).
  - `EfRepository` overrides Ardalis's protected `SaveChangesAsync` to a **no-op** so the inherited `AddAsync`/`UpdateAsync`/`DeleteAsync` track-only. Reads are unaffected.
- `TransactionBehavior` becomes the single owner of the flush + commit:
  1. Run the handler (entities are now staged, not saved).
  2. Find the **single dirty** `IModuleDbContext` (`ChangeTracker.HasChanges()`). If none, no transaction is needed.
  3. Begin the transaction **on that dirty context**.
  4. `SaveChanges` (flush inserts/updates inside the transaction).
  5. Drain domain events (dispatch → `SaveChanges`) — the existing drain loop, now operating on the correct context.
  6. Commit. On any exception, roll back.
- The existing multi-dirty-context guard (throws if a command touches two modules) stays — it remains the enforcement of "one module per command."

**Why this is safe here:** the usual risk of deferring saves is "I need the DB-generated key immediately." This codebase generates all IDs in the domain (`Guid` via `User.Create`, `Notification.Queue`, etc.), never via DB identity columns, so nothing downstream needs an early flush.

**Outcome:** the comment in `InviteUserCommandHandler.cs:83` ("TransactionBehavior owns the unit-of-work boundary … calls SaveChanges + Commit") becomes literally true, and `ITransactional` commands are atomic for *every* module regardless of registration order.

### 2.2 Correction B — per-module (co-located) transactional outbox

**The current state.** The Wolverine outbox lives in a separate shared `messagingdb`. The envelope insert and the command's data commit are therefore two independent transactions; a crash between them can drop or orphan an event. This is documented as an accepted trade-off in `WolverineIntegrationEventPublisher` and `TODOS.md`.

**Why we change it.** With Correction A in place, `TransactionBehavior` owns one real transaction on the module's own database. That is exactly the seam the textbook transactional outbox needs: if the envelope tables live **inside the same module database**, the envelope row and the business row commit in the **same transaction** — true atomicity, no cross-DB gap.

**This was partly prototyped here, but we refine the topology.** Commit `8ab369b` ("true transactional outbox co-located per module") proved co-located envelopes work (green Testcontainers tests `RealOutboxWiringTests`, `TransactionalOutboxTests`), but it *dropped `messagingdb` entirely* and made `identitydb` the main store. That is the wrong topology for a system that grows to many modules: it turns `identitydb` into a snowflake that hosts every other module's messaging plumbing (inbox, dead-letter, scheduled, durable local queues), permanently coupling them and making Identity un-extractable. We re-derive on top of Correction A with a **better, hybrid topology** (decided during eng review, 2026-06-23).

**Key insight: atomicity and shared infrastructure are two separate needs; only one needs co-location.**
- **Outbox envelopes** must commit atomically with the business write → live **co-located** inside each module's own DB.
- **Shared in-flight machinery** (inbox dedup, durable local queues, scheduled messages, dead-letter, node/agent coordination) is never tied to a single business write → has **no reason** to live in a module DB.

**The fix (hybrid topology).**
```
identitydb       -> Identity data        + Identity outbox envelopes      (atomic, ancillary store)
notificationsdb  -> Notifications data    + Notifications outbox envelopes (atomic, ancillary store)
<module>db       -> module data           + module outbox envelopes        (atomic, ancillary store)
messagingdb      -> ONLY shared infra: inbox, local queues, scheduled, DLQ, agents (MAIN store)
   (owned by the host/gateway — messaging is a host concern, not a domain concern)
```
- **Keep `messagingdb`, change its role.** It stays the Wolverine **main** store, but now holds *only* shared infra — no outbox envelopes. (The old design put *all* envelopes here, which is what made it non-atomic.)
- Map Wolverine envelope storage into each module `DbContext` (`MapWolverineEnvelopeStorage` in `OnModelCreating`) so each module DB is registered as an **ancillary** store holding *only its own* outbox tables.
- Provision each ancillary store's envelope schema explicitly at startup (`IMessageStore.Admin.MigrateAsync`); the main store auto-builds. (The `8ab369b` groundwork commit flagged ancillary provisioning as the fiddly part and solved it — use as reference.)
- `WolverineIntegrationEventPublisher` keeps enrolling the active module DbContext — now correct, because that context's outbox tables are co-located with its transaction.
- Update the publisher XML-doc and `AGENTS.md`/`ARCHITECTURE.md`: the cross-DB non-atomicity caveat is **removed** (envelopes are now atomic with data); document that `messagingdb` holds shared infra only and that module DBs host their own outbox tables.

**Why this beats both prior designs:** full atomicity (envelopes co-located) *and* clean module boundaries (no module's plumbing inside another's DB), and it stays symmetric as modules are added.

**Cost we accept (documented for learning):** the most wiring of any option — one main store plus N ancillary stores, each needing explicit envelope-schema provisioning at startup. This is the deliberate, correct place to spend the "innovation token."

**Reference for learning:** `git show 8ab369b` is the prior working implementation; use it as a study aid, not a copy source.

---

## 3. Findings → fixes (the full backlog)

Each item lists the finding, the fix, and the verification. Severities carry over from the review.

### Phase 0 — Foundation (Section 2)
- **F1 (Critical):** UoW fix A — §2.1.
- **F3-base / Outbox (High):** per-module transactional outbox — §2.2.

### Phase 1 — Correctness bugs

- **F2 (High) — Validation failures return 500 instead of 400.**
  `ValidationBehavior` is registered outside `DomainExceptionBehavior` (`GatewayServiceCollectionExtensions.cs:73-77`), so its `FluentValidation.ValidationException` is never caught by the behavior's `catch (ValidationException)` (dead code), and `ErrorHandlingMiddleware` has no case for it → `500`.
  **Fix:** reorder so `DomainExceptionBehavior` wraps `ValidationBehavior` (register it immediately after `LoggingBehavior`), **and** add a defensive `FluentValidation.ValidationException → 400` mapping in `ErrorHandlingMiddleware` that projects the field errors into ProblemDetails. Belt-and-suspenders: the behavior maps to `Result.Invalid` for the mediator path; the middleware covers any exception that escapes the pipeline.
  **Verify:** integration test posting an invalid body asserts `400` + problem details; unit test asserts `DomainExceptionBehavior` maps `ValidationException → Result.Invalid`.

- **F3-dispatcher (High) — Dispatcher publishes fire-and-forget outside a transaction.**
  `NotificationDispatcher.cs:144,156` saves `MarkDelivered` then publishes `NotificationDeliveredIntegrationEvent` via the raw `IMessageBus`.
  **Fix:** route the delivery event through `IIntegrationEventPublisher` inside a unit of work so it enrolls in the (now co-located) outbox atomically with the state change. The dispatcher must run its state transition + publish inside a transaction on `NotificationsDbContext`.
  **Verify:** Testcontainers test — a crash/throw after `MarkDelivered` but before commit delivers nothing and leaves the row not-Delivered.

- **F4 (High) — Notifications can stick in `Dispatched` forever.**
  Status is set `Dispatched` and saved before `SendAsync` (`NotificationDispatcher.cs:114-115`); the due-query selects only `Pending` (`DueNotificationsSpecification.cs:11-12`). A crash in between strands the row.
  **Fix:** introduce a `Claimed`/`Dispatching` state with a **reclaim timeout**: the due-query selects `Pending` OR (`Dispatching` AND `claimed_at < now − timeout`). On success → `Delivered`; on failure → `Pending`/`Failed` per attempt budget. (Aligns with the `TODOS.md` P1 note.)
  **Verify:** test that a row left `Dispatching` past the timeout is re-selected and delivered; a fresh `Dispatching` row is not.

- **F5 (Critical) — SignalR JWT-from-query-string not wired.**
  `AddJwtBearer` (`IdentityPortalAuthenticationBuilderExtensions.cs:32-49`) never reads `?access_token=`, so authenticated browser hub connections to `/hubs/app` get 401.
  **Fix:** add `OnMessageReceived` that copies `Request.Query["access_token"]` into `context.Token` when the path starts with `/hubs`.
  **Verify:** unit test on the configured event; manual/integration check that a hub connect with a query-string token authenticates.

### Phase 2 — Security & remaining Medium bugs

- **F6 (High) — Temp password persisted in plaintext in the outbox.**
  `InvitationCreatedDomainEventHandler.cs:34` embeds the temp password in the integration-event body → stored at rest in the envelope table.
  **Fix (chosen: minimal — eng review 2026-06-23):** keep generating + emailing a temp password, but **keep it out of the integration-event body**. The credential must travel via a direct, non-persisted path (the notification send), never serialized into a durable outbox envelope. The invitation aggregate must not expose `TempPassword` on its transported/event surface.
  *(The ideal required-action/set-password-link flow was considered and deferred — see Eng Review Decisions. Because we keep a generated password, **F-pwpolicy stays in scope.**)*
  **Verify:** test that the published integration event / envelope contains no password; the temp password still reaches the user via the (non-persisted) email path.

- **F-idemp (Medium) — Consumer has no idempotency.**
  `NotificationRequestedIntegrationEventConsumer` creates a new `Notification` (`Guid.NewGuid()`) on every (re)delivery → duplicate emails on retry.
  **Fix:** dedupe on the integration event's id (or `CorrelationId`) with a unique index; ignore an already-processed event. With the co-located inbox, prefer Wolverine's inbox dedup where it applies.
  **Verify:** redelivering the same event produces exactly one notification.

- **F-swallow (Medium) — `QueueNotificationCommandHandler` swallows all exceptions.**
  `:32-60` wraps everything in `try/catch → Result.Error(ex.Message)`: leaks internal messages, commits on failure, and defeats the consumer's transient/permanent classification.
  **Fix:** remove the blanket catch. Let domain/validation failures surface as `Result.Invalid` (via the pipeline) and infrastructure faults propagate so the transaction rolls back and the consumer can classify correctly.
  **Verify:** a malformed recipient yields `Result.Invalid` (permanent, dropped), a transient DB fault propagates (retryable).

- **F-compensate (Medium) — Invite compensation can't fire for commit-time failures.**
  The handler's `try/catch` (`InviteUserCommandHandler.cs:52-108`) only covers in-memory work; the commit happens later in the behavior, so the realistic failure path never triggers the Keycloak cleanup.
  **Fix:** move compensation to react to the *transaction* outcome. With F6 (required-action flow) the external side-effect is smaller; remaining external effects (Keycloak user creation) should be compensated via an outbox-driven failure handler or a post-commit hook, not the in-handler catch. Document the saga boundary.
  **Verify:** simulate a commit failure → Keycloak user is cleaned up (or a compensation message is enqueued).

- **F-identity (Medium) — `InvitedByUserId` / audit columns store the Keycloak `sub`.**
  `InviteUserEndpoint.cs:35` persists the external id as `CreatedByUserId`; `AuditableEntityInterceptor` stamps `CreatedBy`/`LastModifiedBy` from the JWT `sub` (`TODOS.md` PR#4 note). Violates the local-UUID-is-canonical convention.
  **Fix (eng review 2026-06-23):** resolve `sub → local Guid` via `IUserExternalIdResolver` at the JWT boundary (endpoint) before persisting `InvitedByUserId`. For audit columns, **resolve once per request and cache** in a scoped service (the current user is fixed for a request); `AuditableEntityInterceptor` reads the cached local Guid. This gives one lookup per request, not per save — correct identity at minimal cost.
  **Verify:** invitation persists a local Guid creator; audit columns hold local Guids; the resolver is hit at most once per request (cache test).

- **F-https (Medium) — `RequireHttpsMetadata` never set.**
  Defaults to `true`; breaks dev auth against HTTP Keycloak once a dev `ClientId` is supplied.
  **Fix:** thread the environment into `AddIdentityPortals` and set `options.RequireHttpsMetadata = !isDevelopment`.
  **Verify:** dev config with HTTP authority loads metadata; non-dev requires HTTPS.

- **F-tokens (High, template mechanics) — `{{ProjectName}}`/`{{ProjectNameLower}}` not wired.**
  They reach runtime values (Redis `InstanceName` cache-key prefix `GatewayServiceCollectionExtensions.cs:101,107`; Keycloak realm default `:268`, `appsettings.json:112`), and seed subjects (`NotificationsModuleExtensions.cs:119-123`), and email templates.
  **Fix:** add `symbols` (with `replaces`) in `.template.config/template.json` for both tokens (or convert to `sourceName`-derived values). Cover with the `dotnet new` smoke test in `CLAUDE.md`.
  **Verify:** scaffold a project; assert no literal `{{...}}` remains.

- **F-emailinj (Medium) — Email subject never CRLF/encoding-sanitized; `actionUrl` no scheme check.**
  `NotificationContentBuilder.cs:50` injects raw metadata into the subject; `:71,82` emits `actionUrl` without scheme validation (`javascript:` possible).
  **Fix:** strip CR/LF from the subject; treat metadata as untrusted; validate `actionUrl` scheme is http/https before emitting.
  **Verify:** metadata with CRLF/`javascript:` is neutralised.

- **F-paging (Medium) — `ListUsers` discards `TotalCount`.**
  `ListUsersQueryHandler.cs:20` drops the count; `ListUsersEndpoint.cs:42` returns a bare collection.
  **Fix:** return a paged envelope (or `X-Total-Count` header) carrying total + page metadata.
  **Verify:** response exposes total count and page info.

- **F-pwpolicy (Medium) — temp password generation may violate Keycloak policy.**
  **In scope** (F6 keeps the generated password). `RandomNumberGenerator.GetString` can emit a password missing a required character class, causing intermittent Keycloak rejection. **Fix:** guarantee at least one of each required class, then shuffle.
  **Verify:** generator output always contains ≥1 of each required class (statistical/loop test).

### Phase 3 — DRY & code smells

- **D1 — Three email senders duplicate from/reply-to/html-vs-text construction.** Extract a shared `EmailEnvelope` + base options consumed by `MailKitEmailSender`/`ResendEmailSender`/`SendGridEmailSender`.
- **D2 — `ExecuteFailureAsync` duplicated for `Result` and `Result<T>`** (`Web/ArdalisResultHttpExtensions.cs:44-67`). Share one private mapper.
- **D3 — User-id claim resolution duplicated** across `Web/ClaimsPrincipalExtensions.cs`, `HttpContextCurrentUserProvider.cs`, `RealTime/AppHub.cs` with inconsistent fallbacks. Consolidate into one helper in `Identity.Abstractions`.
- **D4 — Escaped-ILIKE idiom duplicated** in `UserRepository`/`InvitationRepository`. Extract a shared `IQueryable` email-match extension.
- **D5 — `KeycloakDirectoryClient` god class** (~365 lines). Split role operations into a `KeycloakRoleClient`; factor the `EnsureSuccessStatusCode`+parse boilerplate.
- **D6 — `PerformanceBehavior` is a no-op trace.** Add a configurable slow-request threshold that logs a warning; otherwise remove it.
- **D7 — `NotificationDailyDigestJob` is a no-op.** Either implement a real digest or rename to reflect that it monitors stale-pending counts. **Decision needed at implementation** (default: rename + document, since a real digest is a feature).
- **D8 — `EmailSenderDispatcher` silently falls back to MailKit/localhost in prod.** Fail fast when no provider is configured in non-Development.
- **D9 — Redundant `?? "admin"` after `GetSecret`** in `AppHost.cs:165-166,205-206`. Remove dead defaulting.
- **D10 — `ValidationBehavior` redundant null filter** (`:29`). Remove.
- **D11 — Dead/parallel `EfRepository` abstraction.** Resolved by Correction A (repos now actually use it).

### Phase 4 — Consistency & primary-constructor sweep

- **C1 — Adopt primary constructors codebase-wide** (review finding; .NET 10 / C# 13). Convert classic ctor + `_field` assignment classes to primary constructors across handlers, middleware, publishers/providers, email senders, jobs, health checks, and repositories (e.g. `class UserRepository(IdentityDbContext db) : EfRepository<IdentityDbContext, User>(db)`). Keep classic ctors only where there is non-trivial ctor logic (e.g. Guard chains that read better explicitly). Document the chosen convention in `AGENTS.md` §7.
- **C2 — Correct the interceptor-wiring comments** (`GatewayModuleRegistrationExtensions.cs:31-33`, `SharedKernelInterceptorExtensions.cs:11-14`) that falsely claim EF auto-discovers interceptors.
- **C3 — Middleware order:** move `ErrorHandlingMiddleware` to the outermost position (`GatewayApplicationExtensions.cs:15-17`) so exceptions in `SecurityHeaders`/`CorrelationId` are still rendered as ProblemDetails.
- **C4 — Rate limiter:** emit `application/problem+json` on rejection; apply or remove the unused named `"api"` policy. Set `NameClaimType = "preferred_username"` (and map Keycloak roles) so `User.Identity.Name` and any future `[Authorize(Roles=...)]` work.

---

## 4. Sequencing & dependencies

```
Phase 0  Foundation
  F1 (UoW fix A)  ──►  enables  ──►  Outbox (per-module)  ──►  enables  ──►  F3-dispatcher
        │
        └─ must land first; everything transactional depends on it

Phase 1  Correctness   (F2, F4, F5  independent; F3-dispatcher needs Phase 0)
Phase 2  Security/Med  (F6 before F-compensate; F-tokens independent)
Phase 3  DRY/smells    (independent; D11 already done by F1)
Phase 4  Consistency   (C1 sweep last to avoid churn-conflicts with earlier edits)
```

Rationale: Phase 0 changes the transaction model that Phases 1–2 rely on; doing the primary-constructor sweep (C1) last avoids re-touching files mid-flight.

---

## 5. Testing strategy

- **Per-finding failing test first** (`AGENTS.md` §2; TDD). Each fix is gated on a test that fails on `main` and passes after.
- **Foundation needs live infra.** UoW atomicity and the co-located outbox are verified with **Testcontainers Postgres** (mirroring the existing `RealOutboxWiringTests`/`TransactionalOutboxTests` from `8ab369b`), because SQLite/in-memory cannot prove cross-context transaction + outbox atomicity.
- **Critically: add a two-module test fixture.** The UoW bug hid because tests register one module at a time. Add an integration fixture that registers **both** modules so an Identity command proves its transaction lands on `identitydb` (this is the regression test for F1).
- **Definition of Done:** `dotnet build` (0 warnings) + `dotnet test` (all green) for the whole solution before any phase is considered complete.

---

## 6. Risks & open decisions

- **Wolverine multi-store wiring** (ancillary store provisioning) is the highest-risk part of Phase 0. Mitigation: re-derive from the proven `8ab369b`, gate on the resurrected Testcontainers tests.
- **Deferring SaveChanges** could surprise any code that relied on an early flush. Audit confirms IDs are domain-generated; still, run the full suite after the repo change before proceeding.
- **F-compensate / saga boundary** is the least mechanical fix; its exact shape (post-commit hook vs. outbox-driven compensation) is finalized during planning once the outbox is in place.
- **D7 (digest)**: implement vs. rename — defaulting to rename+document unless you want the digest feature.
- **F6 required-action flow** depends on Keycloak realm configuration supporting UPDATE_PASSWORD required actions (it does by default); confirm during implementation.

---

## 7. Definition of done (whole effort)

- All review findings closed or explicitly deferred with a rationale in `TODOS.md`.
- `ITransactional` atomicity and integration-event atomicity proven by Testcontainers tests, including a **two-module** fixture.
- Validation failures return `400`.
- Docs (`AGENTS.md`, `ARCHITECTURE.md`, `TODOS.md`, code comments) match the code; the cross-DB non-atomicity caveat is removed.
- `dotnet build` clean (warnings-as-errors) + `dotnet test` green.
- No AI co-author trailer on any commit (`CLAUDE.md`/`AGENTS.md`).

---

## 8. Eng Review Decisions (2026-06-23)

Outcomes of `/plan-eng-review`. These are binding on implementation.

1. **Landing strategy: Phase 0 ships as its own PR, verified green, before Phases 1-4.** Phases 1-4 follow as separate stacked PRs off the new base. Rationale: keep the high-risk transaction/outbox rewrite out of the same diff as the codebase-wide mechanical sweep; each PR independently reviewable and revertible.
2. **Outbox topology: hybrid (see §2.2 rewrite).** Keep `messagingdb` as the Wolverine **main** store holding *only* shared infra (inbox, durable local queues, scheduled, DLQ, agents). Co-locate **outbox envelopes** per module as **ancillary** stores. Do **not** drop `messagingdb` (corrects `8ab369b`). Reason: scales cleanly to many modules without turning one module DB into a snowflake.
3. **Test infrastructure: add Testcontainers.** Foundation atomicity (UoW + outbox) is proven against real Postgres; Docker becomes a prerequisite for integration tests / CI. SQLite stays for non-foundation tests.
4. **F6: minimal fix** (keep temp password, remove from outbox). Ideal required-action flow deferred. **F-pwpolicy stays in scope.**
5. **F-identity: per-request cached `sub → local Guid` resolution** for audit columns (one lookup per request).
6. **Migration advisory-lock (TODOS P1) bundled into Phase 0**, while we are already rewriting startup DB provisioning for ancillary stores. (Implementer may isolate it in its own commit within the Phase 0 PR.)

### Sequencing dependencies (hard)
- **F-swallow** (remove blanket catch) lands **after F2** (so `ValidationException → Result.Invalid` keeps the consumer's permanent/transient classification correct).
- **D5** (`KeycloakDirectoryClient` split) lands **after F6** (both edit that class; avoid entangling).
- **C1** (primary-constructor sweep) is **isolated to the Phase 4 PR** — Phases 0-3 do **no** opportunistic constructor conversions (never mix structural + behavioral changes in one diff).
- Conflict flag: F6 (Lane C) and D5 (Lane D) both touch `KeycloakDirectoryClient` → keep sequential.

### Test gaps to add (now plan requirements)
1. **Topology placement** (new from decision 2): Identity envelope lands in `identitydb`; `messagingdb` holds only infra.
2. **F-pwpolicy** generator class-coverage test (back in scope from decision 4).
3. **D1/D2/D3** behavior-preserving **regression** tests for the DRY refactors.
4. **C3** middleware-order: throw in `SecurityHeaders` still renders as `problem+json`.
5. **F5** optional `→E2E` real hub-connect auth.
6. **F-compensate** test is **blocked** on the saga-shape decision (§6 open item) — finalize design first.

### NOT in scope (added during review)
- F6 ideal required-action/set-password-link flow (deferred; minimal chosen).
- `AddDbContextPool → AddNpgsqlDbContext` Aspire enrichment (TODOS P2) unless it blocks ancillary-store wiring.
- `AzureBlobStorage` SAS/URL (TODOS P3).
- Prompt outbox flush (TODOS P2) — revisit after Phase 0.
- D7 digest: default to rename+document, not a digest feature (revisit in Phase 3).

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Codex Review | `/codex review` | Independent 2nd opinion | 0 | — | — |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | ISSUES_RESOLVED | 5 decisions, 1 TODO bundled, 8 test gaps added, 1 blocked (F-compensate) |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | — | — |

**UNRESOLVED:** F-compensate saga shape (blocked on design, §6) — not a review decision, a planning item.
**VERDICT:** ENG REVIEW COMPLETE — spec updated with all decisions; ready to turn into an implementation plan (Phase 0 first).
</content>
</invoke>
