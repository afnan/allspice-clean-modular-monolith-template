# Phase 4 — Consistency & Primary-Constructor Sweep Implementation Plan

> Final phase. Stacks on merged Phase 3 (`main`). One Phase 4 PR → independent review → fix → merge.

**Goal:** Close the consistency findings (C1–C4) from the 2026-06-23 review. C1 is the isolated codebase-wide primary-constructor sweep (kept last to avoid churn conflicts with Phases 0–3).

**Constraints:** .NET 10 / C# 13, warnings-as-errors, central package versions, no AI co-author trailer. C1 is behaviour-preserving (gated on the full suite). C3/C4 are behavioural and get tests.

## Items

- **C2** — Fix the stale comments in `GatewayModuleRegistrationExtensions.cs` and `SharedKernelInterceptorExtensions.cs` that claim EF Core auto-discovers interceptors. They are registered explicitly via `TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, …>())`; the comment should say so.
- **C3** — Move `ErrorHandlingMiddleware` to the outermost position in `UseGatewayPipeline` so exceptions thrown in `SecurityHeaders`/`CorrelationId` are still rendered as ProblemDetails. Currently it's registered 3rd. **Test:** a throw in `SecurityHeadersMiddleware` is rendered as `application/problem+json`.
- **C4** — Rate limiter: emit `application/problem+json` on rejection (currently plain text); remove the unused named `"api"` fixed-window policy (dead — global limiter is used); set `NameClaimType = "preferred_username"` and map Keycloak realm roles to role claims so `User.Identity.Name` and `[Authorize(Roles=…)]` work. **Tests:** rejection content-type; claim/role mapping where unit-testable.
- **C1** — Adopt primary constructors across all ~55 eligible classes (handlers, middleware, behaviors, publishers/providers, repositories, email senders, jobs, health checks, services).
  - **Style A (pure assignment):** drop the field, reference the parameter directly (e.g. `class UserRepository(IdentityDbContext db) : EfRepository<IdentityDbContext, User>(db)`).
  - **Style B (`.Value` unwrap / Guard / computed):** primary ctor param + a field initializer (e.g. `private readonly TOptions _options = options.Value;`, `private readonly HttpClient _httpClient = Guard.Against.Null(httpClient);`).
  - Document the chosen convention in `AGENTS.md` §7.

## Sequencing
C2 (doc) → C3 (+test) → C4 (+test) → C1 sweep (last; partitioned by project, built and full-suite-gated in batches) → AGENTS.md convention. C1 touches middleware classes but not the static `Gateway*Extensions` files that C3/C4 edit, so no conflict.

## Definition of Done
Build clean (0 warnings) + full suite green (Docker for Testcontainers). One Phase 4 PR; independent review; fix; merge. This closes the correctness-overhaul effort.
