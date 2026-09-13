# Backend convention checklist (.NET modular monolith)

Full rule set for this repository (and any project generated from the
allspice-modular template). `AGENTS.md` and `ARCHITECTURE.md` at the repo root are
the source of truth; if this checklist drifts from them, they win. Recipes
(`Recipe:`) are ready-made searches; run them instead of eyeballing when a rule is
mechanically detectable. A recipe hit is a lead, not a verdict: read the code
before flagging.

The dependency rule is the frame: Api -> Application -> Domain, and
Infrastructure -> Application/Domain. **Nothing inner references anything outer.**
Domain references nothing external at all (no EF, HTTP, or infrastructure types).

## Domain layer (the core)

- Entities derive from `Entity` / `AuditableEntity` / `SoftDeletableEntity`
  (SharedKernel). Aggregate roots carry the `IAggregateRoot` marker; repositories
  operate only on roots. Typed keys: `Entity<TId>` with a
  `readonly record struct XxxId(Guid Value)` plus an EF `HasConversion`.
- **Encapsulation:** `private` constructor + static factory (`Create...`). Child
  entities in a `private readonly List<T>` exposed as `IReadOnlyCollection<T>`.
  Mutate only through aggregate methods. No repository for a child entity.
- **Do NOT use primary constructors on aggregates/entities.** A primary
  constructor is a public constructor: it bypasses the factory and guards. Primary
  constructors are for DI types only (handlers, services, repositories, endpoints,
  behaviors).
  Recipe: in Domain folders, search `class\s+\w+\s*\(` on types deriving from
  `Entity`, `AuditableEntity`, or `SoftDeletableEntity`. Any hit is
  `issue (blocking)`.
- **Invariants** enforced with `Ardalis.GuardClauses` in factories/methods. A rule
  break throws a **`DomainException` subtype** (`BusinessRuleViolationException`,
  `ConflictException`, ...), which maps to the right HTTP status plus a
  machine-readable `code`. Never a bare `Exception` or `InvalidOperationException`.
  Recipe: search the diff for `throw new Exception(` and
  `throw new InvalidOperationException(` in Domain and Application. Hits are
  `issue (blocking)`.
- **Value objects** derive from `ValueObject` (money, address, email). Immutable,
  equality by value. Flag primitive obsession on identifiers and domain
  quantities.
- **Smart enums** (`Ardalis.SmartEnum`) for closed sets that carry behavior,
  display text, or metadata. Raw `enum` only for trivial internal flags. Flag a
  raw enum surfaced to the API or switched on for behavior.
- **Time is injected.** Aggregate methods take an explicit `nowUtc` parameter; the
  handler passes `TimeProvider.GetUtcNow()`. Never read the clock directly in
  domain, application, or infrastructure code.
  Recipe: search the diff for `DateTime.Now`, `DateTime.UtcNow`,
  `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`. Hits outside the composition
  root and tests are `issue (blocking)`.
- **Domain events** raised via `RegisterDomainEvent(...)` (pass `nowUtc`), handled
  in-process by a same-module `IDomainEventHandler<T>`. Domain events never cross
  modules (see Messaging).

## Repositories and Ardalis.Specification (query logic has one home)

- **Bespoke repository per aggregate:** `IXxxRepository` extending Ardalis
  `IRepository<T>` / `IReadRepository<T>`, implemented as
  `XxxRepository : EfRepository<TContext, T>`. Handlers depend on the **bespoke**
  interface, never on raw `IRepository<T>` / `IReadRepository<T>` at the call
  site, and never on the `DbContext` directly.
- Query predicates live in **specification classes** exposed through bespoke
  repository methods, never inline in handlers or endpoints.
  Recipe: flag `.Where(`, `.Include(`, `.OrderBy(` chained on a DbSet inside a
  handler; flag `DbContext` as a handler dependency.
- **Before flagging or writing a new spec, search existing ones.** If a spec with
  the same predicate exists, the finding is "reuse the existing spec".
- Flag the **same predicate duplicated** across two or more handlers or specs
  (DRY): extract or reuse one specification.
- SQL LIKE/ILIKE from user input: escape with `StringExtensions.EscapeLikePattern`
  and the 3-arg `EF.Functions.ILike(col, pattern, "\\")` overload. Flag raw
  interpolation of user input into a pattern.

## Application layer (use cases)

- One folder per feature: `Command`/`Query` + `Handler` + `Validator`
  (`Application/Features/...`). Commands and queries are `record`s. One handler is
  one use case (SRP).
- **Mutating commands implement `ITransactional`.** That is what gives the
  transaction, pre-commit domain-event dispatch, and outbox enrollment. Flag a
  handler that writes state whose command does not implement `ITransactional`.
- **One module's `DbContext` per command.** A command reads and writes exactly one
  module's context (`TransactionBehavior` throws on two). Cross-module work goes
  through integration events, never a direct write.
- Handlers return **`Ardalis.Result` / `Result<T>`** for expected outcomes
  (`.NotFound`, `.Invalid`, `.Conflict`, `.Forbidden`). Genuinely exceptional
  states throw the `DomainException` subtype. No exceptions as control flow.
- **FluentValidation** validators check input shape (run by `ValidationBehavior`).
  Domain invariants stay in the aggregate. Do not duplicate the same rule in both
  places (see DRY).
- Manual mapping domain -> DTO (no AutoMapper/Mapster). One mapper per aggregate,
  not scattered. List queries paginate (`PagedResponse`); flag unbounded list
  endpoints.

## DRY (common duplication smells)

Flag duplication where the second copy can drift from the first:

- Mapping logic written inline when a mapper for that aggregate already exists, or
  the same mapping in two places.
- The same validation rule in both a FluentValidation validator and the
  aggregate's guards. Input shape belongs to the validator, invariants to the
  aggregate; pick the right home, not both.
- The same query predicate in two handlers or two specs.
- Repeated magic strings: permission keys, error codes, connection names. They
  belong in the constants class that exists for them.
- Copy-pasted handler or endpoint blocks that differ by one identifier.

Balance: **KISS/YAGNI still applies.** Do not demand an abstraction for two lines
that coincidentally look alike. Flag duplication of *knowledge* (a rule, a
predicate, a contract), not duplication of *typing*. Flag speculative abstraction
and premature generality as hard as duplication.

## Infrastructure layer (the details)

- One `DbContext` per module; entity configurations in
  `Persistence/Configurations`.
- **Soft delete:** `ISoftDelete` entities are soft-deleted automatically
  (`SoftDeleteInterceptor` turns `Remove()` into a flag + user stamp;
  `SoftDeleteQueryFilterConvention` hides deleted rows). **Soft delete does not
  cascade:** if a soft-deletable aggregate owns children, the children must be
  `ISoftDelete` too or be removed explicitly, otherwise `Remove()` soft-deletes
  the parent and hard-deletes the children. Flag a new soft-deletable aggregate
  whose children are not.
- UTC enforced by `UtcDateTimeOffsetConvention`.
- **EF interceptors are attached explicitly.** EF Core does not auto-discover
  DI-registered `IInterceptor`s. New cross-cutting interceptors register in
  `AddSharedKernelInterceptors`; the module's SP-aware
  `AddDbContextWithWolverineIntegration<T>` registration attaches them. Flag an
  interceptor registered in DI but never attached.
- Each module exposes `Add{Module}ModuleServices` and
  `Ensure{Module}ModuleDatabaseAsync` extensions.
- Migrations run at startup (`MigrationRunner.RunForModuleAsync<TContext>`): they
  must be idempotent and must not crash startup. Generated migration code is
  otherwise exempt from style rules.

## Messaging (domain vs integration events)

- In-process, same module = **domain event** (`RegisterDomainEvent` +
  `IDomainEventHandler<T>`).
- Cross-module = **integration event** via `IIntegrationEventPublisher.PublishAsync`
  (durable Wolverine outbox), published **only inside an `ITransactional`
  command** (publishing outside a transaction throws by design). Consumed by a
  Wolverine handler in the target module. Shared event DTOs live in a
  `*.Contracts` library.
- Flag: a module reacting to another module's domain event; cross-module work done
  synchronously; fire-and-forget publishes; a non-idempotent integration-event
  consumer (the outbox can redeliver).

## Event sourcing (opt-in, ADR-0009)

- Is event sourcing **justified** for this aggregate (golden rule 8)? Flag CRUD/reference data, externally
  mirrored data, or PII-heavy aggregates being event-sourced.
- Aggregate derives from `EventSourcedAggregate`; events are `sealed record … : IDomainEvent` with ids /
  amounts / timestamps only — **no personal data**.
- `Apply(TEvent)` methods are pure (no validation, no throws); dispatcher is `When`, never `Apply`.
- Bespoke `IXxxRepository : IEventSourcedRepository<Xxx>`; handlers `LoadAsync` → mutate → `SaveAsync`.
  No direct `IDocumentSession`/`LightweightSession()` in handlers.
- Reads use projections; only an explicit history/audit query touches the stream.
- Changed an event's shape? Requires a new stored name (`MapEventType`) + `Upcast` + tests; never an edit.
- Marten appears only in `Infrastructure` (architecture test); `ApplyEventStoreSchemaAsync` is called in `Ensure…`.
- Tests: Domain given/when/then; Testcontainers integration for round trip + concurrency + projection.

## Api layer (the edge)

- **FastEndpoints** (not MVC controllers). The endpoint maps request ->
  command/query, sends via `IMediator.Send`, returns the raw DTO on 2xx.
- **Result failures map with `result.ExecuteFailureAsync(HttpContext)`** (status
  -> RFC-7807 ProblemDetails). Do not hand-roll the status switch, wrap responses
  in an envelope (`ApiResponse<T>`), or catch domain exceptions per endpoint (the
  `DomainException` pipeline behavior handles them).
- **Endpoint assembly registration:** auto-discovery is off. A new module's Api
  assembly must be added to the `Assemblies` array in
  `GatewayServiceCollectionExtensions`. Flag a new endpoint whose assembly is not
  registered.
- **Async all the way:** `CancellationToken` threaded through endpoint -> handler
  -> repository -> EF call. `Async` suffix on async methods. `await using` for
  disposables. No scoped service or `DbContext` captured beyond its scope.
  Recipe: search the diff for `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`.
  Hits outside tests are `issue (blocking)`.

## Authorization (one model, ADR-0008)

- Gate endpoints with `Policies(PermissionPolicy.For("module:area.action"))` or
  `[HasPermission("module:area.action")]`. No `AllowAnonymous` on data endpoints.
  Recipe: search changed endpoint files for `AllowAnonymous` and for `Configure()`
  bodies with no `Policies(` / `[HasPermission` at all.
- **Every permission key a module enforces must be declared in its
  `IModulePermissionManifest`.** That is how the reconciler seeds it and how
  `PermissionKeyConsistencyTests` verifies it. A new `Policies(PermissionPolicy.For(...))`
  key with no manifest entry is `issue (blocking)`. Absence claim: search the
  module's manifest for the key before flagging.
- Per-entity ownership/tenant/status checks go through
  `IResourceAuthorizer.AuthorizeAsync(entity, AuthorizationActions.X, ct)` inside
  the handler, mapping `Result.Forbidden()` to 403. Keep `HttpContext` out of
  handlers.
- **Never a second authorization mechanism** (`[Authorize(Roles=...)]`, custom
  middleware). One model only.
- Gotcha: inside an endpoint's `Configure()`, the static permission-constants
  class `Permissions` shadows FastEndpoints' `Permissions(...)` method; resolve
  with a using alias or `PermissionPolicy.For("...")` literal.

## Identity and security

- **Local user `Guid` is the canonical identity.** Keycloak external IDs only at
  the JWT/SignalR boundary and Keycloak admin calls; convert with
  `IUserExternalIdResolver`. Flag an external ID stored or compared where a local
  `Guid` belongs.
- **No secrets in source** (including AppHost dev defaults that leak to non-dev
  and design-time factories). Use `GetSecret(...)`, user-secrets, env vars,
  `EF_DESIGN_*`.

## Modular monolith structure (boundary violation checks)

- Each module lives under `Services/{Module}/` with
  `Domain / Application / Infrastructure / Api`, hosted in-process by the API
  gateway.
- **No direct project reference between two modules.** Cross-module sharing goes
  through `Shared/` libraries (`SharedKernel`, `ApiContracts`, `*.Contracts`,
  `Identity.Abstractions`).
  Recipe: in changed `.csproj` files, search `<ProjectReference` for a path from
  one `Services/` module to another. Hit is `issue (blocking)`.
- **New module wiring is a five-step recipe**; a PR adding a module must have all
  five or it is `chore (blocking)`: module extensions
  (`Add{Module}ModuleServices` + `Ensure{Module}ModuleDatabaseAsync`), a line in
  `RegisterGatewayModules()`, an `Ensure...` call in `Program.cs`, the Api
  assembly in the `Assemblies` array, the Aspire database resource in `AppHost.cs`
  plus a design-time `DbContextFactory`.
- **Nothing module-specific goes into the shared kernel**, and the shared kernel
  references no module.

## Modern C# and language conventions

- **Primary constructors** for DI-injected types (handlers, services,
  repositories, endpoints, behaviors, jobs, health checks), keeping
  `private readonly` fields initialized from the parameters so method bodies read
  the field. Classic constructor only when the body does real work (FluentValidation
  `RuleFor` setup, loops, side effects). Never primary constructors on
  aggregates/value objects (see Domain).
- `record` for commands, queries, DTOs, value-object-like data. `sealed` by
  default. File-scoped namespaces; private fields `_camelCase`. Nullable reference
  types on (flag `!` null-forgiving without cause). Collection expressions,
  target-typed `new`, pattern matching over type checks.
- Package versions live in `Directory.Packages.props`, never inline.
  Recipe: search changed `.csproj` files for `Version=` on a `PackageReference`.
  Hit is `issue (blocking)`.
- `TreatWarningsAsErrors` is global. No blanket `NoWarn`; a genuinely external
  advisory gets a narrow, documented `NuGetAuditSuppress` in
  `Directory.Build.props`.

## Tests (mandatory)

- **Every new use case (handler) has a unit test** (xUnit + Moq) in the matching
  `{Module}.{Layer}.UnitTests` project: the success `Result` path and the
  failure/validation paths. Domain logic (aggregates, value objects, smart enums,
  specifications) is unit-tested. A PR that adds a use case without a test is
  `chore (blocking)`. Absence claim: search the test projects for
  `{Handler}Tests` before flagging.
- **Architecture and fitness tests** in `Architecture.Tests` must stay green and
  cover new rules (layering, module isolation, `PermissionKeyConsistencyTests`,
  interceptor wiring). A new module or new layering rule should be covered there.
- Don't assert success from a green compile alone; the Definition of Done is
  build clean (0 warnings) plus all tests green.
