# Phase 1 Correctness Implementation Plan

> Stacks on merged Phase 0 (`main`). Same flow: TDD per task → PR → independent review → fix → merge.

**Goal:** Fix the Phase 1 correctness bugs from the 2026-06-23 review: validation→500, SignalR auth, dispatcher fire-and-forget, stuck `Dispatched`.

**Tech stack / constraints:** .NET 10, warnings-as-errors, central package versions, no AI co-author trailer, Definition of Done = build clean + tests green. No primary-constructor sweep (Phase 4).

## Global constraints
- One module DbContext per command; integration events via `IIntegrationEventPublisher` inside a transaction.
- Tests: unit where possible; Testcontainers for outbox/DB-atomic behavior.

---

## Task 1 — F2: validation failures return 400, not 500

**Files:** `ApiGateway/Extensions/GatewayServiceCollectionExtensions.cs` (pipeline order), `ApiGateway/Middleware/ErrorHandlingMiddleware.cs` (belt-and-suspenders mapping), tests in `SharedKernel.UnitTests` + a gateway middleware test.

**Root cause:** `ValidationBehavior` is registered *outside* `DomainExceptionBehavior`, so its `FluentValidation.ValidationException` is never mapped to `Result.Invalid`; `ErrorHandlingMiddleware` has no case for it → 500.

- [ ] Step 1: Failing unit test — `DomainExceptionResultMapper.MapToResult<Result>(new ValidationException(...))` returns `Result.Invalid` with the field errors. (If it already does, add a middleware test instead.)
- [ ] Step 2: Reorder pipeline so `DomainExceptionBehavior` wraps `ValidationBehavior`: register `DomainExceptionBehavior` immediately after `LoggingBehavior` (order: Logging → DomainException → Performance → Validation → Transaction).
- [ ] Step 3: Add `FluentValidation.ValidationException => HttpStatusCode.BadRequest` to `ErrorHandlingMiddleware.HandleExceptionAsync`, projecting `ex.Errors` into the ProblemDetails (defensive: covers any escape).
- [ ] Step 4: Unit-test `ErrorHandlingMiddleware` maps a thrown `ValidationException` to 400 + problem+json.
- [ ] Step 5: Build clean + tests green. Commit.

## Task 2 — F5: SignalR JWT from query string

**Files:** `Shared/Identity.Abstractions/Authentication/IdentityPortalAuthenticationBuilderExtensions.cs`, test in a suitable unit project.

**Root cause:** browsers can't send an `Authorization` header on WebSocket/SSE; the JWT must be read from `?access_token=` for `/hubs` paths. Not wired → every authenticated hub connection 401s.

- [ ] Step 1: Failing unit test — build the `JwtBearerOptions` via the extension, invoke `OnMessageReceived` with a `MessageReceivedContext` whose request path is `/hubs/app` and query `access_token=abc`; assert `context.Token == "abc"`. For a non-hub path, assert token not set from query.
- [ ] Step 2: In each `AddJwtBearer`, set `options.Events.OnMessageReceived` to copy `context.Request.Query["access_token"]` into `context.Token` when `context.HttpContext.Request.Path.StartsWithSegments("/hubs")`.
- [ ] Step 3: Build clean + tests green. Commit.

## Task 3 — F4: reclaim stuck `Dispatched` notifications

**Files:** `Notifications/Domain/Aggregates/Notification.cs` (+ a `DispatchedUtc`/claim timestamp if not present), `Notifications/Domain/Specifications/DueNotificationsSpecification.cs`, `NotificationDispatcherOptions.cs` (reclaim timeout), `NotificationDispatcher.cs`, a migration, tests.

**Root cause:** status is set `Dispatched` + saved before `SendAsync`; the due-query selects only `Pending`. A crash in between strands the row forever.

- [ ] Step 1: Failing test — a notification left `Dispatched` with a dispatch timestamp older than the reclaim timeout is re-selected by `DueNotificationsSpecification`; a fresh `Dispatched` one is not.
- [ ] Step 2: Ensure `Notification` records when it was marked `Dispatched` (add `DispatchedUtc` if absent; `MarkDispatched` stamps it).
- [ ] Step 3: Widen `DueNotificationsSpecification` to select `Pending` OR (`Dispatched` AND `DispatchedUtc < now - reclaimTimeout`), still `AttemptCount < MaxDeliveryAttempts`.
- [ ] Step 4: Add `ReclaimAfterSeconds` to `NotificationDispatcherOptions`; thread it into the spec from the dispatcher.
- [ ] Step 5: EF migration for any new column (Npgsql); verify SQLite tests unaffected.
- [ ] Step 6: Build clean + tests green. Commit.

## Task 4 — F3: dispatcher publishes the delivered event atomically (no fire-and-forget)

**Files:** `NotificationDispatcher.cs`, tests (Testcontainers).

**Root cause:** `await _messageBus.PublishAsync(deliveryEvent)` is fire-and-forget, separate from the `MarkDelivered` save — a crash between loses the event; AGENTS.md forbids fire-and-forget.

- [ ] Step 1: Failing Testcontainers test — when delivery succeeds, the `NotificationDeliveredIntegrationEvent` is persisted to the notifications co-located outbox in the SAME transaction as the `Delivered` status (rollback → neither).
- [ ] Step 2: Replace the raw `IMessageBus.PublishAsync` with the co-located outbox: in the success branch, open a transaction on `NotificationsDbContext`, `MarkDelivered` + save, enrol the context into `IDbContextOutbox`, publish the delivered event, commit. (Mirror `WolverineIntegrationEventPublisher`'s enroll+publish; the dispatcher owns its own UoW here.)
- [ ] Step 3: Build clean + tests green. Commit.

---

## Sequencing
Task 1 (F2) and Task 2 (F5) are independent and small — do first. Task 3 (F4) and Task 4 (F3) both touch the dispatcher; do F4 then F3 (F3's atomic-publish builds on the dispatcher's per-notification transaction shape).

## Definition of Done
Build clean (0 warnings) + full suite green (Docker for Testcontainers). One PR for Phase 1; independent review; fix; merge.
