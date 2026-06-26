# 0003 — CQRS via Mediator with a behavior pipeline

- Status: Accepted
- Date: 2026-06-23

## Context

Handlers need consistent cross-cutting behavior (validation, logging, performance tracking, transactions,
domain-event dispatch, exception→Result mapping) without each handler re-implementing it.

## Decision

Use the source-generated **Mediator** library for CQRS. Each feature is a `Command`/`Query` + `Handler`
(+ `Validator`). State-mutating commands implement the `ITransactional` marker. A pipeline of behaviors wraps
every request, ordered outer→inner: **Logging → Performance → Validation → Transaction → DomainException**.

`TransactionBehavior` owns the unit of work for `ITransactional` commands: it finds the single dirty module
`DbContext`, opens a transaction, runs the handler, drains and dispatches domain events (multi-generation
loop), commits, then flushes the outbox. Handlers return `Ardalis.Result`.

## Consequences

- Cross-cutting concerns are centralized and consistent; handlers stay focused on domain logic.
- Source generation avoids reflection/runtime registration cost.
- Behavior order is load-bearing — changing it can break the validation/exception safety net (pinned by tests).
