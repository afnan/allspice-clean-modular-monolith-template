# 0001 — Modular monolith with a single deployable host

- Status: Accepted
- Date: 2026-06-23

## Context

We need clean module boundaries (independent domains, isolated data) without the operational cost of
microservices (network hops, distributed transactions, per-service deployment/observability) at this stage.

## Decision

Build a **modular monolith**. Each module lives under `Services/{Module}/` with Clean Architecture layers
(Domain / Application / Infrastructure / Api) and owns its own `DbContext` and database. All modules register
**into one runnable host**, the `ApiGateway`; they are not separate processes. `AppHost` (Aspire) orchestrates
infrastructure for local development only.

Module boundaries are enforced: a command touches exactly one module's `DbContext`, and cross-module
communication goes through Wolverine integration events — never a direct cross-module DB write.

## Consequences

- One build, one deploy, in-process calls — simple to run and debug.
- Strong boundaries keep the option open to extract a module into its own service later.
- The boundary rules must be actively enforced (see [0007](0007-architecture-fitness-tests.md)) or the
  monolith erodes into a big ball of mud.
