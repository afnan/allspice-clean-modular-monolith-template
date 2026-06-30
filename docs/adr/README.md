# Architecture Decision Records

Short records of the significant architectural decisions in this project, in
[MADR](https://adr.github.io/madr/)-lite form. They capture **why** — the descriptive "how" lives in
[`ARCHITECTURE.md`](../../ARCHITECTURE.md) and the prescriptive rules in [`AGENTS.md`](../../AGENTS.md).

When you make a decision that changes a pattern here, add a new ADR (supersede, don't rewrite history).

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-modular-monolith-single-host.md) | Modular monolith with a single deployable host | Accepted |
| [0002](0002-co-located-transactional-outbox.md) | Co-located per-module transactional outbox | Accepted |
| [0003](0003-cqrs-mediator-pipeline.md) | CQRS via Mediator with a behavior pipeline | Accepted |
| [0004](0004-bespoke-repository-per-aggregate.md) | Bespoke repository per aggregate | Accepted |
| [0005](0005-local-uuid-canonical-identity.md) | Local UUID is the canonical identity | Accepted |
| [0006](0006-timeprovider-clock.md) | `TimeProvider` for all clock access | Accepted |
| [0007](0007-architecture-fitness-tests.md) | Architecture-fitness tests enforce the golden rules | Accepted |
| [0008](0008-in-app-permission-based-authorization.md) | In-app permission-based authorization (RBAC) | Accepted |
