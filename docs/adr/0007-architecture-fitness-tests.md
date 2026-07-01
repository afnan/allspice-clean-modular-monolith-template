# 0007 — Architecture-fitness tests enforce the golden rules

- Status: Accepted
- Date: 2026-06-26

## Context

The golden rules (domain purity, module isolation, bespoke repositories, layer/naming conventions) were
enforced only at **runtime** (`TransactionBehavior` throws on a multi-DbContext command) and in **code
review**. That is too late and too manual — especially for AI agents generating code, which need a fast,
deterministic signal when they violate the architecture.

## Decision

Add a **`tests/AllSpice.CleanModularMonolith.Architecture.Tests`** project using **NetArchTest** that turns the
golden rules into fitness functions run on every build:

- Domain layers have no dependency on EF Core, ASP.NET, FastEndpoints, Wolverine, Quartz, etc.
- Modules don't depend on each other's internals (only via `*.Contracts`).
- Mediator handlers live in the Application layer; aggregate roots live in the Domain layer.
- Domain events are sealed.

The test **is** the architecture contract: when a rule legitimately changes, the rule is updated here in the
same change.

## Consequences

- Violations fail `dotnet test` (and CI) immediately, not in review.
- Gives AI agents a deterministic guardrail aligned with `AGENTS.md`.
- Rules must be kept in sync with intentional architecture changes (by design).
