using AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
// Marten declares its own ITransactionParticipant; alias SharedKernel's so it resolves unambiguously
// in this file, which also `using Marten;` for IDocumentStore/QuerySession.
using ITransactionParticipant = AllSpice.CleanModularMonolith.SharedKernel.Persistence.ITransactionParticipant;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests;

/// <summary>
/// Proves the enlistment contract on real Postgres: events appended through the repository commit and roll
/// back together with the module DbContext's transaction; FetchForWriting gives optimistic concurrency;
/// archive and history behave as documented. TransactionBehavior itself is unit-tested with a fake
/// participant (SharedKernel.UnitTests); here the REAL participant is driven the way the behavior drives it.
/// </summary>
public sealed class MartenEnlistmentTests(ProbeHostFixture fixture) : IClassFixture<ProbeHostFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_then_load_round_trips_state_and_version()
    {
        var id = Guid.NewGuid();

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            var counter = ProbeCounter.Start(id, Now);
            counter.Increment(5, Now);
            await repo.AddAsync(counter);

            Assert.NotNull(db.Database.CurrentTransaction); // the participant opened the module tx early
            Assert.True(participant.HasPendingChanges);

            await participant.FlushAsync(CancellationToken.None);
            await db.Database.CurrentTransaction!.CommitAsync();

            Assert.Empty(counter.UncommittedEvents);
        }

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var loaded = await repo.LoadAsync(id);

            Assert.NotNull(loaded);
            Assert.Equal(5, loaded.Value);
            Assert.Equal(2, loaded.Version); // two events in the stream
            Assert.Equal(id, loaded.Id);
        }
    }

    [Fact]
    public async Task Load_returns_null_for_an_unknown_stream()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();

        Assert.Null(await repo.LoadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Rollback_of_the_module_transaction_discards_events_and_EF_rows_together()
    {
        var id = Guid.NewGuid();

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            await repo.AddAsync(ProbeCounter.Start(id, Now));
            db.Rows.Add(new ProbeRow { Id = id, Name = "doomed" });
            await db.SaveChangesAsync();
            await participant.FlushAsync(CancellationToken.None);

            await db.Database.CurrentTransaction!.RollbackAsync();
            participant.Discard();
        }

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            Assert.Null(await repo.LoadAsync(id));
            Assert.False(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Rows, r => r.Id == id));
        }
    }

    [Fact]
    public async Task Concurrent_writer_surfaces_as_ConcurrencyConflictException()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var first = fixture.Host.Services.CreateAsyncScope();
        await using var second = fixture.Host.Services.CreateAsyncScope();

        var repo1 = first.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var repo2 = second.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var counter1 = (await repo1.LoadAsync(id))!;
        var counter2 = (await repo2.LoadAsync(id))!;

        counter1.Increment(1, Now);
        await repo1.SaveAsync(counter1);
        await first.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
        await first.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.CommitAsync();

        counter2.Increment(1, Now);
        await repo2.SaveAsync(counter2);
        var participant2 = second.ServiceProvider.GetRequiredService<ITransactionParticipant>();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => participant2.FlushAsync(CancellationToken.None).AsTask());

        await second.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.RollbackAsync();
    }

    [Fact]
    public async Task Domain_events_are_taken_from_tracked_aggregates_and_cleared_after_flush()
    {
        var id = Guid.NewGuid();
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var participant = scope.ServiceProvider.GetRequiredService<ITransactionParticipant>();

        var counter = ProbeCounter.Start(id, Now);
        await repo.AddAsync(counter);
        await participant.FlushAsync(CancellationToken.None);

        var events = participant.TakeDomainEvents().ToList();

        Assert.Single(events);
        Assert.IsType<CounterStarted>(events[0]);
        Assert.Empty(counter.UncommittedEvents);
        Assert.Empty(participant.TakeDomainEvents());
        await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.RollbackAsync();
    }

    [Fact]
    public async Task History_exposes_versions_metadata_and_typed_data()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, increments: 2);

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();

        var history = await repo.HistoryAsync(id);

        Assert.Equal(3, history.Count);
        Assert.Equal([1L, 2L, 3L], history.Select(h => h.Version));
        Assert.IsType<CounterStarted>(history[0].Data);
        Assert.All(history, h => Assert.False(string.IsNullOrEmpty(h.EventType)));
        Assert.All(history, h => Assert.True(h.Sequence > 0));
    }

    [Fact]
    public async Task Retire_archives_the_stream_so_default_queries_exclude_it()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
            var counter = (await repo.LoadAsync(id))!;
            counter.Retire(Now);
            await repo.SaveAsync(counter);
            await scope.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.CommitAsync();
        }

        var store = fixture.Host.Services.GetRequiredService<IProbeEventStore>();
        await using var query = store.QuerySession();
        var state = await query.Events.FetchStreamStateAsync(id);

        Assert.NotNull(state);
        Assert.True(state.IsArchived);
    }

    private async Task SeedAsync(Guid id, int increments = 0)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProbeCounterRepository>();
        var counter = ProbeCounter.Start(id, Now);
        for (var i = 0; i < increments; i++)
        {
            counter.Increment(1, Now);
        }

        await repo.AddAsync(counter);
        await scope.ServiceProvider.GetRequiredService<ITransactionParticipant>().FlushAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.CurrentTransaction!.CommitAsync();
    }
}
