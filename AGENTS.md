# AGENTS.md — Rules for AI agents & contributors

This file is the **operating manual for any AI agent** (Claude Code, Codex, Copilot, Cursor, etc.)
working in this repository. It is prescriptive: it says what to **DO**, what **NOT** to do, and how to
verify your work. For the descriptive "how the system is built" reference, see [`ARCHITECTURE.md`](./ARCHITECTURE.md).

If a rule here conflicts with a direct instruction from the human you're working with, the human wins —
but call out the conflict.

---

## 0. Golden rules (do not violate)

1. **One module's `DbContext` per command.** A command/handler may read & write exactly **one** module's
   `DbContext`. Never write to another module's `DbContext`. Cross-module communication goes through
   **Wolverine integration events** via `IIntegrationEventPublisher` — never a direct cross-module DB write.
2. **Publish integration events only inside an `ITransactional` command.** `IIntegrationEventPublisher`
   enrolls the active module DbContext transaction. The outbox envelope tables are **co-located in each
   module's own database**, so the envelope commits **atomically** with the state change (a true
   transactional outbox; the shared `messagingdb` holds only Wolverine infrastructure). Publishing outside a
   transaction throws by design. No fire-and-forget integration events.
3. **Local user `Guid` is the canonical identity.** Keycloak external IDs are only for Keycloak admin calls
   and the JWT/SignalR boundary. Resolve between them with `IUserExternalIdResolver`. Don't store or compare
   external IDs where a local `Guid` is expected.
4. **Bespoke repository per aggregate.** Define `IXxxRepository` (extending Ardalis `IRepository<T>`/
   `IReadRepository<T>`) and `XxxRepository : RepositoryBase<T>`. Handlers depend on the **bespoke** interface,
   never on a raw `IRepository<T>` at the call site.
5. **No secrets in source.** Use the AppHost `GetSecret(...)` helper (throws in non-Development) and
   `dotnet user-secrets` / env vars / `--parameter`. Design-time factories read `EF_DESIGN_*` and fail fast.
6. **Definition of Done = build clean (warnings are errors) + tests pass.** See §2. Never claim "done"
   without running both.
7. **Commit messages carry no AI co-author trailer.** Do not add `Co-Authored-By: Claude ...` or any AI
   attribution. Branch before committing if you're on `main`; only commit/push when asked.

---

## 1. Architecture in 30 seconds

- **.NET 10 modular monolith.** One runnable host: `AllSpice.CleanModularMonolith.ApiGateway`. Modules
  register **into** it; they are not separate processes. `AppHost` is the Aspire orchestrator (Postgres,
  Redis, Keycloak, Papercut SMTP, Azurite).
- **Each module** lives under `Services/{Module}/` with Clean Architecture layers:
  `Domain / Application / Infrastructure / Api`.
- **Flow:** FastEndpoint → `IMediator.Send(Command/Query)` → Handler (pipeline behaviors) → bespoke
  Repository (Ardalis.Specification) → module `DbContext`.
- **Pipeline order:** Logging → Performance → Validation → Transaction → DomainException.
- Full detail lives in [`ARCHITECTURE.md`](./ARCHITECTURE.md). Read it before designing anything non-trivial.

---

## 2. Build, run, test (your Definition of Done)

```bash
dotnet build AllSpice.CleanModularMonolith.slnx          # MUST be 0 warnings (TreatWarningsAsErrors=true)
dotnet test  AllSpice.CleanModularMonolith.slnx          # MUST be all green
dotnet run --project AllSpice.CleanModularMonolith.AppHost/AllSpice.CleanModularMonolith.AppHost.csproj   # full stack via Aspire
```

- Warnings are errors. If you hit a genuinely external/transitive advisory, suppress it **narrowly** with a
  documented `NuGetAuditSuppress` entry in `Directory.Build.props` — never blanket-disable auditing or add
  broad `NoWarn`.
- Run a single test project: `dotnet test tests/<Project>`. Filter: `--filter "FullyQualifiedName~Name"`.
- Don't assert success from a green compile alone — run the tests and read the output.

---

## 3. DO — patterns by layer

**Domain** (`Services/{Module}/Domain`)
- Model entities from `Entity` / `AuditableEntity` / `SoftDeletableEntity` (SharedKernel); mark aggregate roots
  with the `IAggregateRoot` marker interface (repositories operate only on `IAggregateRoot`). Audit is a
  concern (`IAuditable`) independent of root-ness — a root may be `Entity, IAggregateRoot` or `AuditableEntity, IAggregateRoot`.
- Validate invariants with **Ardalis.GuardClauses** in factories/methods; raise domain events via
  `RegisterDomainEvent`. Keep domain logic free of EF/HTTP/infrastructure types.
- Use `Ardalis.SmartEnum` for closed sets; value objects derive from `ValueObject`.

**Application** (`Application/Features/...`)
- One folder per feature: `Command`/`Query` + `Handler` + `Validator`. Commands that mutate state implement
  `ITransactional`. Handlers return `Ardalis.Result` / `Result<T>`.
- Validate inputs with **FluentValidation** (runs in `ValidationBehavior`). Guard handler args with GuardClauses.
- Query objects use **Ardalis.Specification**; expose them through bespoke repository methods.

**Infrastructure** (`Infrastructure/...`)
- One `DbContext` per module; configurations in `Persistence/Configurations`. Soft delete is automatic for
  `ISoftDelete` entities: `Remove()` is turned into a soft delete by `SoftDeleteInterceptor` (sets `IsDeleted` +
  stamps the user) and `SoftDeleteQueryFilterConvention` hides deleted rows (`IgnoreQueryFilters()` to include
  them). **Soft delete does not cascade:** if a soft-deletable aggregate owns child entities, make the children
  `ISoftDelete` too (or remove them explicitly) — otherwise `Remove()` soft-deletes the parent but hard-deletes
  the children. UTC is enforced by `UtcDateTimeOffsetConvention`.
- Cross-cutting EF interceptors are attached through the SP-aware pooled registration (see §6); register new
  ones via `AddSharedKernelInterceptors` or the module's `AddDbContextPool((sp, options) => ...AddInterceptors(...))`.
- Each module exposes `Add{Module}ModuleServices` + `Ensure{Module}ModuleDatabaseAsync` extensions.

**Api** (`Api/Endpoints`)
- Use **FastEndpoints** (not MVC controllers). Map `Result` failures with
  `result.ExecuteFailureAsync(HttpContext)` (status → RFC7807 ProblemDetails) instead of hand-rolling the switch.
- Register the endpoint assembly in `GatewayServiceCollectionExtensions` (auto-discovery is off).

---

## 4. DON'T — anti-patterns (these get rejected in review)

- ❌ Writing to another module's `DbContext`, or a single command touching two `DbContext`s
  (`TransactionBehavior` throws on this).
- ❌ Publishing integration events outside an `ITransactional` command, or using domain events for
  cross-module communication (domain events are in-process, same-module only).
- ❌ Depending on `IRepository<T>` / `IReadRepository<T>` directly in a handler — always the bespoke interface.
- ❌ Treating a Keycloak external ID as a local identity (or vice versa).
- ❌ Building SQL `LIKE`/`ILIKE` patterns from raw user input — escape with `StringExtensions.EscapeLikePattern`
  and the 3-arg `EF.Functions.ILike(col, pattern, "\\")` overload (`_`/`%` are wildcards, valid in emails).
- ❌ Hardcoded passwords/secrets/connection strings (including design-time and AppHost dev defaults that leak
  to non-dev).
- ❌ Pinning package versions in individual `.csproj` files — versions live in `Directory.Packages.props`.
- ❌ MVC controllers, broad `catch`-and-swallow, blanket `NoWarn`.
- ❌ Reading the clock directly (`DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.UtcNow`) in domain/application/
  infrastructure code. Inject **`TimeProvider`** and call `GetUtcNow()`; in domain aggregates take an explicit
  `nowUtc` timestamp parameter sourced from it (so time is deterministic and testable). The only literal
  `TimeProvider.System` lives at the composition root (`AddSharedKernelInterceptors`).
- ❌ Relying on EF Core to auto-discover DI-registered `IInterceptor`s — it does **not**; attach explicitly (§6).
- ❌ Adding a Claude/AI `Co-Authored-By` trailer to commits.

---

## 5. Recipes

**Add a module**
1. Create `Services/{Module}/{Domain,Application,Infrastructure,Api}`.
2. Write `Infrastructure/Extensions/{Module}ModuleExtensions.cs` with `Add{Module}ModuleServices(builder, logger)`
   and `Ensure{Module}ModuleDatabaseAsync(app)`.
3. Add one line to `RegisterGatewayModules()` and one `Ensure...` call in `Program.cs`.
4. Add the module's API assembly to the `Assemblies` array in `GatewayServiceCollectionExtensions`.
5. Add the Aspire database resource in `AppHost.cs` and a design-time `DbContextFactory`.

**Add a command/query** — create the feature folder (Command/Query + Handler + Validator). Mutations implement
`ITransactional`. Return `Result`/`Result<T>`. Add an endpoint that calls `IMediator.Send` and maps with
`ExecuteFailureAsync`.

**Talk to another module** — publish an integration event via `IIntegrationEventPublisher.PublishAsync(...)`
from inside an `ITransactional` command; consume it with a Wolverine handler in the target module. Put shared
event DTOs in a `*.Contracts` library.

**Model a rich aggregate (DDD checklist)** — the template ships only Identity + Notifications (deliberately no
sample business domain). When you add your own aggregate, follow this shape:
- **Identity:** base on `Entity`/`AuditableEntity`/`SoftDeletableEntity`; mark the root `IAggregateRoot`. For a
  typed key, derive from `Entity<TId>` with a `readonly record struct XxxId(Guid Value)` and map it in EF with
  `builder.Property(x => x.Id).HasConversion(id => id.Value, v => new XxxId(v))`.
- **Encapsulation:** `private` ctor + static factory (e.g. `Order.CreateDraft(...)`); child entities held in a
  `private readonly List<T>` exposed as `IReadOnlyCollection<T>`; mutate only through aggregate methods.
- **Invariants:** enforce with `Ardalis.GuardClauses` in the factory/methods; throw a `DomainException` subtype
  (`BusinessRuleViolationException`, `ConflictException`, …) for rule breaks — they map to the right HTTP status
  + a machine-readable `code`.
- **Value objects** derive from `ValueObject` (e.g. `Money`); **closed sets** use `Ardalis.SmartEnum`.
- **Time:** aggregate methods take an explicit `nowUtc` (the handler passes `TimeProvider.GetUtcNow()`).
- **Events:** raise an in-process domain event via `RegisterDomainEvent(...)` (pass `nowUtc`); a same-module
  `IDomainEventHandler<T>` may translate it into a cross-module integration event via `IIntegrationEventPublisher`
  (only inside an `ITransactional` command). Add a bespoke `IXxxRepository` + `Ardalis.Specification` queries.

**Add a migration**
```bash
EF_DESIGN_DB_PASSWORD=<local-pg-pw> dotnet ef migrations add <Name> \
  --project Services/AllSpice.CleanModularMonolith.<Module>/AllSpice.CleanModularMonolith.<Module>.csproj \
  --startup-project AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
  --context <Module>DbContext --output-dir Infrastructure/Migrations
```

---

## 6. Cross-cutting utilities (use these instead of reinventing)

| Need | Use |
|---|---|
| Result → HTTP ProblemDetails | `result.ExecuteFailureAsync(HttpContext)` (`Web`) |
| Argument/invariant validation | `Ardalis.GuardClauses` |
| Request validation | FluentValidation + `ValidationBehavior` |
| Transaction + domain-event dispatch | implement `ITransactional` (pre-commit dispatch in `TransactionBehavior`) |
| Cross-module messaging | `IIntegrationEventPublisher` (durable Wolverine outbox) |
| Local↔external id | `IUserExternalIdResolver` |
| Current user for audit | `ICurrentUserProvider` (HttpContext/claims impl in the gateway) |
| Current time / clock | injected `TimeProvider` (`GetUtcNow()`); domain methods take an explicit `nowUtc` |
| Audit stamping | `AuditableEntityInterceptor` (auto-wired via `AddSharedKernelInterceptors`) |
| Concurrency debugging | `ConcurrencyDiagnosticInterceptor` (auto-wired) |
| DB connectivity health | `DbContextHealthCheck<TContext>` |
| File/blob storage | `IFileStorageService` + `FileStorageServiceExtensions.CreateFileStorageService` (Azurite/Azure Blob) |
| Bounded log/response text | `StringExtensions.Truncate` |
| Startup migrations w/ retry | `MigrationRunner.RunForModuleAsync<TContext>` |

**Attaching a new EF interceptor:** register it in `AddSharedKernelInterceptors` (as a singleton `IInterceptor`).
It is applied because each module registers its `DbContext` with the **SP-aware** Wolverine overload:
`AddDbContextWithWolverineIntegration<T>((sp, options) => options.UseNpgsql(cs).AddInterceptors(sp.GetServices<IInterceptor>()))`.
EF Core does **not** auto-discover DI interceptors — this explicit attach is mandatory. (That registration also
co-locates the module's Wolverine outbox tables; it is not pooled and does not layer Aspire's
`EnrichNpgsqlDbContext` resilience/telemetry — a deferred enrichment item in `TODOS.md`.) `DomainEventDispatchInterceptor` exists as an **opt-in** post-commit alternative and is
deliberately **not** wired (it would double-dispatch with `TransactionBehavior`).

---

## 7. Conventions

- File-scoped namespaces; private fields `_camelCase`; constants `PascalCase`.
- **Primary constructors** for dependency-injected services, handlers, middleware, behaviors, repositories,
  senders, jobs, and health checks. Keep `private readonly` fields and initialize them from the primary-ctor
  parameter so method bodies read from the field, not the captured parameter:
  ```csharp
  public sealed class Foo(IBar bar, IOptions<FooOptions> options) : IFoo
  {
      private readonly IBar _bar = bar;
      private readonly FooOptions _options = options.Value;        // .Value unwrap in the initializer
  }
  // repositories: class UserRepository(IdentityDbContext db) : EfRepository<IdentityDbContext, User>(db)
  ```
  Keep a **classic constructor** only when the body does more than field initialization (FluentValidation
  `RuleFor` setup, loops, conditionals, side effects). Domain aggregates/value objects keep their
  invariant-encoding constructors.
- Central package versioning (`Directory.Packages.props`); `TreatWarningsAsErrors=true`.
- Tests: xUnit + Moq + coverlet, named `{Module}.{Layer}.UnitTests` / `IntegrationTests`.

---

## 8. When unsure

- Prefer the existing pattern in a sibling module over inventing a new one.
- If a change spans modules, stop and model it as an integration event, not a shortcut.
- State assumptions and verify against the code; don't guess at framework behavior — prove it with a test
  (e.g., the interceptor wiring is backed by `SaveInterceptorDiscoveryTests`).
