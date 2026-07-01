# Enterprise hardening — implementation plan (2026-06-26)

Goal: close the last gaps against the template's three pillars — clean-arch/DDD quality,
AI-readiness, and enterprise-grade ops. Definition of Done per item: `dotnet build` clean
(warnings-as-errors) + `dotnet test` green. Keep `AGENTS.md` / `ARCHITECTURE.md` in sync.

Branch: `feat/enterprise-hardening`.

## Phase A — Foundational kernel (do first)
- [x] A1. `TimeProvider` clock abstraction. Full sweep across SharedKernel interceptors, both modules'
  aggregates/handlers/jobs/services/health checks. Domain methods take explicit `nowUtc`. `TimeProvider.System`
  registered in `AddSharedKernelInterceptors`. Mandated in AGENTS.md. 165 tests green.

## Phase B — Architecture fitness tests
- [x] B1. `tests/...Architecture.Tests` (NetArchTest, 9 tests): domain purity, module isolation, handlers in
  Application, aggregates in Domain, sealed domain events. Runs via `dotnet test` + CI.

## Phase C — Enterprise cross-cutting
- [x] C1. Machine-readable `code` in RFC7807 ProblemDetails; auto-derived from `DomainException` type name.
- [x] C2. PII/secret redaction in `LoggingBehavior` via `[SensitiveData]`; responses no longer logged.
- [x] C3. HTTP idempotency (`Idempotency-Key`) middleware backed by `IDistributedCache` (Redis; memory fallback).
- [~] C4. Output-cache policies exist; Redis is now genuinely used (idempotency). Applying `[CacheOutput]` to a
  specific read endpoint deferred (auth endpoints need careful VaryBy — do per real read). DEFERRED.
- [~] C5. API versioning — DEFERRED (route-shape change needs live verification; document convention first).
- [x] C6. Reusable `ToPaginationResultAsync` paged-query helper on the read repository.

## Phase D — Rich domain module (flagship exemplar)
- [x] D1. DROPPED per user (2026-06-26): don't ship a concrete business domain (e.g. Ordering) into every
  generated project. Delivered the value instead as an AGENTS.md **"Model a rich aggregate (DDD checklist)"**
  recipe — strongly-typed IDs, encapsulation, invariants, value objects, time, domain→integration events.

## Phase E — Ops / deployment
- [x] E1. Multi-stage `Dockerfile` + `.dockerignore`.
- [x] E2. `/health` + `/alive` now mapped in every environment (k8s probes) — `ServiceDefaults` fixed.
- [x] E3. `deploy/k8s/gateway.yaml` (Deployment + Service, probes, security context) + `deploy/README.md`.

## Phase F — CI / supply chain / onboarding / docs
- [x] F1. CI: coverage report (ReportGenerator) + vulnerable-package gate; arch tests run via `dotnet test`.
- [x] F2. `.github/dependabot.yml` (NuGet grouped + Actions + Docker).
- [x] F3. Real `.http` (Identity/Notifications/health/idempotency) + `keycloak/realm-import.json` (+ README) +
  GETTING_STARTED shortcut. (DB seed left as-is — Identity is IdP-provisioned; Notifications templates already seed.)
- [x] F4. `docs/adr/` — index + 7 ADRs (5 existing decisions + TimeProvider + arch tests).
- [x] F5. AGENTS.md / ARCHITECTURE.md / README updated. GETTING_STARTED deploy note DEFERRED with F3.
- [x] F6. No new shipped-vs-maintainer split needed (plan lives under excluded docs/superpowers/).
- [x] F7. `dotnet new` smoke test — verifying.
