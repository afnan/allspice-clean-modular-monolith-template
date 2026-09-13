using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using JasperFx.Events.Projections;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// The ADR-0009 guarantee, end to end through the REAL <see cref="TransactionBehavior{TRequest,TResponse}"/>:
/// an event-sourced append, an ordinary EF row and an integration-event envelope commit atomically — and when
/// the command fails, none of the three exist and nothing is delivered. Mirrors <see cref="OutboxAtomicityTests"/>
/// but drives the behavior instead of a hand-rolled transaction.
/// <para>
/// Two failure shapes are covered because they exercise different code paths in the behavior: a handler
/// exception or a failure <see cref="Result"/> both fail BEFORE the transaction is ever flushed (nothing was
/// physically written, so "nothing persisted" holds trivially — these prove the early-opened transaction and
/// participant are released, not that a real write gets rolled back); a domain-event handler failing via
/// <see cref="SwitchableDomainEventDispatcher"/> fails AFTER the EF row and event-store append have been
/// flushed to the (still uncommitted) transaction — the real atomicity proof.
/// </para>
/// </summary>
public sealed class EventSourcedOutboxAtomicityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = _postgres.GetConnectionString();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContextWithWolverineIntegration<EsProbeDbContext>(o => o.UseNpgsql(cs));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<EsProbeDbContext>());
        builder.AddModuleEventStore<IEsProbeStore, EsProbeDbContext>(cs, "esprobe", opts => opts.Projections.LiveStreamAggregation<Tally>());
        builder.Services.AddScoped<TallyRepository>();
        // Singleton so a test can flip ThrowWhen on the SAME instance the scope resolves IDomainEventDispatcher
        // to — lets one test simulate a domain-event handler failing AFTER the transaction's flush, which a
        // no-op dispatcher can never exercise.
        builder.Services.AddSingleton<SwitchableDomainEventDispatcher>();
        builder.Services.AddSingleton<IDomainEventDispatcher>(sp => sp.GetRequiredService<SwitchableDomainEventDispatcher>());
        builder.Services.AddScoped<IPostCommitActions, PostCommitActions>();
        builder.Services.AddScoped<IOutboxFlusher, TestOutboxFlusher>();
        builder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(cs, "wolverine");
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(typeof(EventSourcedOutboxAtomicityTests).Assembly);
        });
        _host = builder.Build();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EsProbeDbContext>().Database.EnsureCreatedAsync();
        }

        await _host.Services.ApplyEventStoreSchemaAsync<IEsProbeStore>();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Successful_command_commits_stream_row_and_envelope_together()
    {
        var id = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(id);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var behavior = CreateBehavior(scope);
            var result = await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
            {
                var repo = scope.ServiceProvider.GetRequiredService<TallyRepository>();
                var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
                var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

                await repo.AddAsync(Tally.Start(id), ct);               // opens the module tx early
                db.Marks.Add(new Mark { Id = id, Note = "committed" });
                outbox.Enroll(db);                                        // same shape as WolverineIntegrationEventPublisher
                await outbox.PublishAsync(new ProbeEvent(id));
                return Result.Success();
            }, CancellationToken.None);

            Assert.Equal(ResultStatus.Ok, result.Status);
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(delivered == signal.Task, "Integration event was not delivered after commit.");

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.True(await verify.ServiceProvider.GetRequiredService<EsProbeDbContext>().Marks.AnyAsync(m => m.Id == id));
        Assert.NotNull(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    /// <summary>
    /// Covers the PRE-FLUSH cleanup path: the handler throws inside <c>next()</c>, before
    /// <see cref="TransactionBehavior{TRequest,TResponse}"/> ever calls <c>db.SaveChangesAsync()</c> or flushes
    /// the participant, so nothing was physically written yet — the append, the EF row and the envelope only
    /// ever existed as staged/in-memory state. "No row / no stream / not delivered" therefore holds trivially;
    /// what this test actually proves is that the early-opened module transaction gets released and the
    /// participant's staged work discarded rather than leaking into a later command sharing the scope. The
    /// real post-flush rollback is proven separately by
    /// <see cref="Domain_event_handler_failure_after_flush_rolls_back_stream_row_and_envelope"/>.
    /// </summary>
    [Fact]
    public async Task Handler_failure_after_append_persists_nothing_and_delivers_nothing()
    {
        var id = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(id);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var behavior = CreateBehavior(scope);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
                {
                    var repo = scope.ServiceProvider.GetRequiredService<TallyRepository>();
                    var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
                    var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

                    await repo.AddAsync(Tally.Start(id), ct);
                    db.Marks.Add(new Mark { Id = id, Note = "doomed" });
                    outbox.Enroll(db);
                    await outbox.PublishAsync(new ProbeEvent(id));
                    throw new InvalidOperationException("business failure after staging everything");
                }, CancellationToken.None));

            var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
            var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();
            Assert.Null(db.Database.CurrentTransaction);
            Assert.False(participant.HasPendingChanges);
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        Assert.False(delivered == signal.Task, "Integration event was delivered despite a failed command.");

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<EsProbeDbContext>().Marks.AnyAsync(m => m.Id == id));
        Assert.Null(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    /// <summary>
    /// Covers the PRE-FLUSH cleanup path: the handler returns a failure <see cref="Result"/> (rather than
    /// throwing) after only staging the event-store append — <c>TransactionBehavior</c> never reaches
    /// <c>db.SaveChangesAsync()</c>/participant flush for a non-Ok/Created/NoContent result, so nothing was
    /// physically written yet. What this test proves is that the early-opened module transaction is released
    /// and the participant's staged append discarded on this "failure via Result" branch specifically (a
    /// different code path in <c>TransactionBehavior</c> than the thrown-exception branch above). The real
    /// post-flush rollback is proven separately by
    /// <see cref="Domain_event_handler_failure_after_flush_rolls_back_stream_row_and_envelope"/>.
    /// </summary>
    [Fact]
    public async Task Failure_result_after_append_persists_nothing()
    {
        var id = Guid.NewGuid();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var behavior = CreateBehavior(scope);
            var result = await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
            {
                await scope.ServiceProvider.GetRequiredService<TallyRepository>().AddAsync(Tally.Start(id), ct);
                return Result.Conflict("changed my mind");
            }, CancellationToken.None);

            Assert.Equal(ResultStatus.Conflict, result.Status);

            var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
            var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();
            Assert.Null(db.Database.CurrentTransaction);
            Assert.False(participant.HasPendingChanges);
        }

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.Null(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    /// <summary>
    /// The real post-flush atomicity proof: unlike the two tests above, this handler returns SUCCESS, so
    /// <see cref="TransactionBehavior{TRequest,TResponse}"/> physically flushes the EF row and the Marten
    /// append (<c>StartStream</c> persisted inside the transaction) before draining domain events. The
    /// dispatch of <see cref="TallyStarted"/> is made to throw at that point via
    /// <see cref="SwitchableDomainEventDispatcher"/> — a failure AFTER real writes hit the transaction. Proves
    /// the behavior rolls back a transaction that had already been flushed to, not just one that was never
    /// written to.
    /// </summary>
    [Fact]
    public async Task Domain_event_handler_failure_after_flush_rolls_back_stream_row_and_envelope()
    {
        var id = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(id);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<SwitchableDomainEventDispatcher>();
            dispatcher.ThrowWhen = e => e is TallyStarted started && started.TallyId == id;
            try
            {
                var behavior = CreateBehavior(scope);
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await behavior.Handle(new EsProbeCommand(), async (_, ct) =>
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<TallyRepository>();
                        var db = scope.ServiceProvider.GetRequiredService<EsProbeDbContext>();
                        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

                        await repo.AddAsync(Tally.Start(id), ct);
                        db.Marks.Add(new Mark { Id = id, Note = "flushed-then-doomed" });
                        outbox.Enroll(db);
                        await outbox.PublishAsync(new ProbeEvent(id));
                        return Result.Success();
                    }, CancellationToken.None));

                Assert.Null(scope.ServiceProvider.GetRequiredService<EsProbeDbContext>().Database.CurrentTransaction);
            }
            finally
            {
                dispatcher.ThrowWhen = null;
            }
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        Assert.False(delivered == signal.Task, "Integration event was delivered despite a post-flush domain-event handler failure.");

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<EsProbeDbContext>().Marks.AnyAsync(m => m.Id == id));
        Assert.Null(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

    private static TransactionBehavior<EsProbeCommand, Result> CreateBehavior(AsyncServiceScope scope) =>
        new(scope.ServiceProvider.GetServices<IModuleDbContext>(),
            scope.ServiceProvider.GetServices<ITransactionParticipant>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>(),
            scope.ServiceProvider.GetServices<IOutboxFlusher>(),
            scope.ServiceProvider.GetRequiredService<IPostCommitActions>(),
            NullLogger<TransactionBehavior<EsProbeCommand, Result>>.Instance);

    private sealed record EsProbeCommand : Mediator.IMessage, ITransactional;

    /// <summary>Releases the outbox after commit, like the gateway's WolverineOutboxFlusher.</summary>
    private sealed class TestOutboxFlusher(IDbContextOutbox outbox) : IOutboxFlusher
    {
        public async ValueTask FlushAsync(CancellationToken cancellationToken) => await outbox.FlushOutgoingMessagesAsync();
    }

    /// <summary>
    /// Registered as the host's <see cref="IDomainEventDispatcher"/> (singleton) so a test can arm
    /// <see cref="ThrowWhen"/> to fail dispatch for a specific event — simulating a domain-event handler that
    /// fails AFTER <see cref="TransactionBehavior{TRequest,TResponse}"/> has already flushed the EF row and the
    /// event-store append to the transaction. Defaults to a no-op, like <c>NoOpDomainEventDispatcher</c>.
    /// </summary>
    private sealed class SwitchableDomainEventDispatcher : IDomainEventDispatcher
    {
        public Func<IDomainEvent, bool>? ThrowWhen { get; set; }

        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            if (ThrowWhen is { } shouldThrow && domainEvents.Any(shouldThrow))
            {
                throw new InvalidOperationException("domain handler failed");
            }

            return Task.CompletedTask;
        }
    }
}

public sealed record TallyStarted(Guid TallyId) : IDomainEvent;

public sealed class Tally : EventSourcedAggregate
{
    private Tally() { }

    public static Tally Start(Guid id)
    {
        var tally = new Tally();
        tally.Raise(new TallyStarted(id));
        return tally;
    }

    public void Apply(TallyStarted e) => Id = e.TallyId;

    protected override void When(IDomainEvent @event)
    {
        if (@event is TallyStarted e) Apply(e);
        else throw new InvalidOperationException($"Unhandled {@event.GetType().Name}");
    }
}

public interface IEsProbeStore : Marten.IDocumentStore { }

public sealed class TallyRepository(IModuleEventStoreSession<IEsProbeStore> session)
    : MartenEventSourcedRepository<Tally, IEsProbeStore>(session);

public sealed class Mark
{
    public Guid Id { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class EsProbeDbContext(DbContextOptions<EsProbeDbContext> options) : DbContext(options), IModuleDbContext
{
    public DbSet<Mark> Marks => Set<Mark>();
    public DbContext Instance => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mark>().HasKey(m => m.Id);
        modelBuilder.MapWolverineEnvelopeStorage("wolverine");
    }
}
