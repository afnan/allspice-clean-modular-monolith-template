# 0004 — Bespoke repository per aggregate

- Status: Accepted
- Date: 2026-06-23

## Context

A single generic `IRepository<T>` at handler call sites leaks persistence concerns into the application layer
and makes intent unclear (any aggregate, any operation). We want explicit, intention-revealing data access.

## Decision

Define a **bespoke repository interface per aggregate** — `IXxxRepository` extending Ardalis
`IRepository<T>` / `IReadRepository<T>` — with an implementation `XxxRepository : EfRepository<TContext, T>`.
Handlers depend on the **bespoke** interface, never on the raw generic at the call site. Query objects use
Ardalis.Specification and are exposed through bespoke repository methods.

Repositories **stage only** (no `SaveChanges`); the unit-of-work flush/commit is owned by `TransactionBehavior`
(see [0003](0003-cqrs-mediator-pipeline.md)).

## Consequences

- Data access is explicit and discoverable per aggregate; easy to mock in handler unit tests.
- One more interface/class per aggregate (small, mechanical cost).
- Enforced by an architecture-fitness test (see [0007](0007-architecture-fitness-tests.md)).
