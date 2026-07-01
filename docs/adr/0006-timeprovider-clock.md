# 0006 — `TimeProvider` for all clock access

- Status: Accepted
- Date: 2026-06-26

## Context

`DateTimeOffset.UtcNow` was read directly in domain aggregates, interceptors, jobs, services, and health
checks. Hardcoded clock reads make time-dependent logic (retry backoff, due-notification windows, token
expiry, audit/freshness stamps) non-deterministic and awkward to unit test.

## Decision

Read the clock through .NET's **`TimeProvider`** abstraction everywhere:

- Infrastructure/application services, interceptors, jobs, and health checks **inject `TimeProvider`** and call
  `GetUtcNow()`.
- Domain aggregate methods take an **explicit `nowUtc` timestamp parameter** sourced from `TimeProvider` by the
  caller (domain stays free of ambient/static clock access). Domain event timestamps are passed in the same way.
- The only literal `TimeProvider.System` registration lives at the composition root
  (`AddSharedKernelInterceptors`).

## Consequences

- Time-dependent behavior is deterministic and unit-testable (pass a fixed `nowUtc`, or a `FakeTimeProvider`).
- Domain factory/mutator signatures gained a `nowUtc` parameter (a one-time, mechanical change).
- The rule is documented in `AGENTS.md` (a DON'T on direct `UtcNow`).
