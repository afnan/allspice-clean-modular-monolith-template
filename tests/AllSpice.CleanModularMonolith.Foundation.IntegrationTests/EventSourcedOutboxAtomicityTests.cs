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
/// the handler fails after appending, none of the three exist and nothing is delivered. Mirrors
/// <see cref="OutboxAtomicityTests"/> but drives the behavior instead of a hand-rolled transaction.
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
        builder.Services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();
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
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        Assert.False(delivered == signal.Task, "Integration event was delivered despite a failed command.");

        await using var verify = _host.Services.CreateAsyncScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<EsProbeDbContext>().Marks.AnyAsync(m => m.Id == id));
        Assert.Null(await verify.ServiceProvider.GetRequiredService<TallyRepository>().LoadAsync(id));
    }

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
        }

        await using var verify = _host.Services.CreateAsyncScope();
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
