using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Behaviors;

/// <summary>
/// An event store joins the module transaction through <see cref="ITransactionParticipant"/>. These tests pin
/// the four contract points: a participant with pending work counts as a dirty module (a transaction is opened
/// and committed even with no EF changes); a transaction the participant opened EARLY is reused, not
/// re-opened; the participant is flushed inside the transaction and its domain events dispatched; and both
/// failure paths (failure Result / exception) discard the participant and roll the early transaction back.
/// Real SQLite so transactions are real.
/// </summary>
public sealed class TransactionBehaviorParticipantTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestModuleDbContext _db;
    private readonly TestModuleDbContext _otherDb;
    private readonly SqliteConnection _otherConnection;

    public TransactionBehaviorParticipantTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TestModuleDbContext(Options(_connection));
        _db.Database.EnsureCreated();
        _db.Database.ExecuteSqlRaw("CREATE TABLE Flushes (Note TEXT NOT NULL)");

        _otherConnection = new SqliteConnection("DataSource=:memory:");
        _otherConnection.Open();
        _otherDb = new TestModuleDbContext(Options(_otherConnection));
        _otherDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _otherDb.Dispose();
        _otherConnection.Dispose();
    }

    [Fact]
    public async Task Participant_with_pending_work_and_no_EF_changes_is_flushed_and_committed()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        var result = await behavior.Handle(new FakeCommand(), (_, _) =>
        {
            participant.Stage("appended"); // no EF entity staged — only the participant is dirty
            return ValueTask.FromResult(Result.Success());
        }, CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(1, participant.FlushCount);
        Assert.Equal(1, await CountFlushesAsync("appended"));
        Assert.Null(_db.Database.CurrentTransaction);
        // The committed module's participants are discarded AFTER the commit: their work was already drained
        // and written at flush, and the session they still hold is bound to a committed, disposed transaction.
        // A later ITransactional command in this same scope must not be handed that stale session.
        Assert.True(participant.Discarded);
    }

    [Fact]
    public async Task Early_opened_transaction_is_reused_not_reopened()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await participant.OpenEarlyAsync(ct); // simulates FetchForWriting needing a session mid-handler
            participant.Stage("early");
            _db.Items.Add(new TestEntity { Name = "row" });
            return Result.Success();
        }, CancellationToken.None);

        Assert.Same(participant.EarlyTransaction, participant.TransactionSeenAtFlush);
        Assert.Equal(1, await CountFlushesAsync("early"));
        await using var fresh = new TestModuleDbContext(Options(_connection));
        Assert.True(await fresh.Items.AnyAsync(i => i.Name == "row"));
        Assert.Null(_db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Participant_domain_events_are_dispatched_and_second_generation_work_is_flushed()
    {
        var participant = new FakeParticipant(_db);
        var dispatched = new List<IDomainEvent>();
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<IDomainEvent> events, CancellationToken _) =>
            {
                dispatched.AddRange(events);
                // a handler reacting to the first event appends more work to the SAME participant
                if (dispatched.Count == 1) participant.Stage("second-generation");
                return Task.CompletedTask;
            });
        var behavior = CreateBehavior(participant, dispatcher.Object);

        await behavior.Handle(new FakeCommand(), (_, _) =>
        {
            participant.Stage("first");
            participant.RaiseDomainEvent(new FakeDomainEvent());
            return ValueTask.FromResult(Result.Success());
        }, CancellationToken.None);

        Assert.Single(dispatched);
        Assert.Equal(2, participant.FlushCount);
        Assert.Equal(1, await CountFlushesAsync("second-generation"));
    }

    [Fact]
    public async Task Failure_result_discards_participant_and_rolls_back_early_transaction()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        var result = await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await participant.OpenEarlyAsync(ct);
            // Written directly through the early transaction (bypassing Stage/FlushAsync, which never runs on
            // this path) so the assertion below can only pass if the early transaction was actually ROLLED
            // BACK — a participant whose FlushAsync never runs proves nothing about commit vs. rollback.
            await participant.WriteNowAsync("rolled-back", ct);
            participant.Stage("never");
            return Result.Conflict("busy");
        }, CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.True(participant.Discarded);
        Assert.Equal(0, participant.FlushCount);
        Assert.Null(_db.Database.CurrentTransaction);
        Assert.Equal(0, await CountFlushesAsync("never"));
        Assert.Equal(0, await CountFlushesAsync("rolled-back"));
    }

    [Fact]
    public async Task Exception_after_flush_rolls_everything_back_and_discards()
    {
        var participant = new FakeParticipant(_db) { ThrowOnFlush = true };
        var behavior = CreateBehavior(participant);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FakeCommand(), (_, _) =>
            {
                _db.Items.Add(new TestEntity { Name = "doomed" });
                participant.Stage("doomed");
                return ValueTask.FromResult(Result.Success());
            }, CancellationToken.None));

        Assert.True(participant.Discarded);
        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Null(_db.Database.CurrentTransaction);
        await using var fresh = new TestModuleDbContext(Options(_connection));
        Assert.False(await fresh.Items.AnyAsync(i => i.Name == "doomed"));
    }

    [Fact]
    public async Task Rollback_with_a_cancelled_token_still_cleans_up_and_surfaces_the_original_exception()
    {
        // A token that gets cancelled the instant the participant's flush fails — simulating a caller-cancelled
        // request racing a genuine domain failure. EF's own BeginTransactionAsync/SaveChangesAsync reject an
        // ALREADY-cancelled token outright (proven separately), so the token must still be live when the
        // handler runs and only flip at the moment of failure, to isolate the behavior under test: does the
        // ROLLBACK step (which used to run with this same, now-cancelled, token) mask the original exception
        // and skip cleanup?
        using var cts = new CancellationTokenSource();
        var participant = new FakeParticipant(_db) { ThrowOnFlush = true, CancelOnFlushThrow = cts };
        var behavior = CreateBehavior(participant);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FakeCommand(), (_, _) =>
            {
                _db.Items.Add(new TestEntity { Name = "doomed-cancelled" });
                participant.Stage("doomed-cancelled");
                return ValueTask.FromResult(Result.Success());
            }, cts.Token));

        // The ORIGINAL exception (from the participant's flush) must surface, not an OperationCanceledException
        // from a rollback call that itself observed the now-cancelled token.
        Assert.Equal("flush failed", ex.Message);
        Assert.True(participant.Discarded);
        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.Null(_db.Database.CurrentTransaction);
        await using var fresh = new TestModuleDbContext(Options(_connection));
        Assert.False(await fresh.Items.AnyAsync(i => i.Name == "doomed-cancelled"));
    }

    [Fact]
    public async Task Successful_commit_releases_a_different_modules_dangling_early_transaction()
    {
        var otherParticipant = new FakeParticipant(_otherDb);
        var behavior = CreateBehavior(otherParticipant, dbContexts: [_db, _otherDb]);

        var result = await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            // Simulates a read/write-intent load on ANOTHER module that opened its transaction early but,
            // this time, ended up with nothing to append — while THIS module's write proceeds and commits.
            await otherParticipant.OpenEarlyAsync(ct);
            _db.Items.Add(new TestEntity { Name = "row" });
            return Result.Success();
        }, CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        await using var fresh = new TestModuleDbContext(Options(_connection));
        Assert.True(await fresh.Items.AnyAsync(i => i.Name == "row"));
        Assert.Null(_db.Database.CurrentTransaction);
        Assert.Null(_otherDb.Database.CurrentTransaction);
        // Releasing that transaction leaves the foreign participant's session bound to a dead transaction, so
        // it is discarded along with it.
        Assert.True(otherParticipant.Discarded);
    }

    [Fact]
    public async Task Only_the_committing_modules_context_has_an_open_transaction_during_the_drain_loop()
    {
        // The integration-event publisher finds the outbox's transaction by looking for "the context with
        // CurrentTransaction != null". Domain-event handlers run inside the drain loop, so a foreign module's
        // early-opened transaction must already be gone by then or that lookup is ambiguous.
        var otherParticipant = new FakeParticipant(_otherDb);
        var participant = new FakeParticipant(_db);
        var openContextsAtDispatch = new List<string>();
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (_db.Database.CurrentTransaction is not null) openContextsAtDispatch.Add("db");
                if (_otherDb.Database.CurrentTransaction is not null) openContextsAtDispatch.Add("otherDb");
                return Task.CompletedTask;
            });
        var behavior = new TransactionBehavior<FakeCommand, Result>(
            [_db, _otherDb], [participant, otherParticipant], dispatcher.Object, [],
            new PostCommitActions(), NullLogger<TransactionBehavior<FakeCommand, Result>>.Instance);

        await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await otherParticipant.OpenEarlyAsync(ct); // foreign module: write-intent load, nothing appended
            participant.Stage("mine");
            participant.RaiseDomainEvent(new FakeDomainEvent());
            return Result.Success();
        }, CancellationToken.None);

        Assert.Equal(["db"], openContextsAtDispatch);
    }

    [Fact]
    public async Task Participant_owner_counts_toward_the_one_module_rule()
    {
        var participant = new FakeParticipant(_otherDb); // participant belongs to ANOTHER module
        var behavior = CreateBehavior(participant, dbContexts: [_db, _otherDb]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FakeCommand(), (_, _) =>
            {
                _db.Items.Add(new TestEntity { Name = "mine" });
                participant.Stage("theirs");
                return ValueTask.FromResult(Result.Success());
            }, CancellationToken.None));

        Assert.Contains("multiple module DbContexts", ex.Message);
        Assert.True(participant.Discarded);
    }

    [Fact]
    public async Task Early_transaction_with_nothing_pending_is_released()
    {
        var participant = new FakeParticipant(_db);
        var behavior = CreateBehavior(participant);

        await behavior.Handle(new FakeCommand(), async (_, ct) =>
        {
            await participant.OpenEarlyAsync(ct); // loaded an aggregate, raised nothing
            return Result.Success();
        }, CancellationToken.None);

        Assert.Null(_db.Database.CurrentTransaction);
        Assert.Equal(0, participant.FlushCount);
    }

    private async Task<int> CountFlushesAsync(string note)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Flushes WHERE Note = $note";
        cmd.Parameters.AddWithValue("$note", note);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private TransactionBehavior<FakeCommand, Result> CreateBehavior(
        FakeParticipant participant,
        IDomainEventDispatcher? dispatcher = null,
        IEnumerable<IModuleDbContext>? dbContexts = null) =>
        new(dbContexts ?? [_db], [participant], dispatcher ?? Mock.Of<IDomainEventDispatcher>(), [],
            new PostCommitActions(), NullLogger<TransactionBehavior<FakeCommand, Result>>.Instance);

    private static DbContextOptions<TestModuleDbContext> Options(SqliteConnection connection) =>
        new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(connection).Options;

    private sealed record FakeCommand : IMessage, ITransactional;

    private sealed record FakeDomainEvent : IDomainEvent;

    /// <summary>
    /// Stands in for the Marten participant: "flushing" writes a row through the owner's CURRENT transaction
    /// (raw SQL on the same connection), so a rollback must make the row vanish and a commit must keep it.
    /// </summary>
    private sealed class FakeParticipant(TestModuleDbContext owner) : ITransactionParticipant
    {
        private readonly List<string> _staged = [];
        private readonly List<IDomainEvent> _events = [];

        public DbContext Owner => owner;
        public bool HasPendingChanges => _staged.Count > 0;
        public int FlushCount { get; private set; }
        public bool Discarded { get; private set; }
        public bool ThrowOnFlush { get; init; }

        /// <summary>When set, cancelled the instant <see cref="ThrowOnFlush"/> fires — simulating a client
        /// cancellation racing a genuine domain failure, so the token is live for every call up to that point.</summary>
        public CancellationTokenSource? CancelOnFlushThrow { get; init; }

        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? EarlyTransaction { get; private set; }
        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? TransactionSeenAtFlush { get; private set; }

        public async Task OpenEarlyAsync(CancellationToken ct) =>
            EarlyTransaction = owner.Database.CurrentTransaction ?? await owner.Database.BeginTransactionAsync(ct);

        public void Stage(string note) => _staged.Add(note);

        public void RaiseDomainEvent(IDomainEvent e) => _events.Add(e);

        /// <summary>Writes a row immediately, through the owner's CURRENT transaction, bypassing Stage/Flush —
        /// for tests that must prove a rollback happened on a path where FlushAsync itself never runs.</summary>
        public Task WriteNowAsync(string note, CancellationToken cancellationToken) =>
            owner.Database.ExecuteSqlRawAsync("INSERT INTO Flushes (Note) VALUES ({0})", [note], cancellationToken);

        public async ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            TransactionSeenAtFlush = owner.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Flush called without a transaction");
            if (ThrowOnFlush)
            {
                CancelOnFlushThrow?.Cancel();
                throw new InvalidOperationException("flush failed");
            }
            foreach (var note in _staged)
            {
                await owner.Database.ExecuteSqlRawAsync("INSERT INTO Flushes (Note) VALUES ({0})", [note], cancellationToken);
            }
            _staged.Clear();
            FlushCount++;
        }

        public IEnumerable<IDomainEvent> TakeDomainEvents()
        {
            var events = _events.ToArray();
            _events.Clear();
            return events;
        }

        public void Discard()
        {
            Discarded = true;
            _staged.Clear();
            _events.Clear();
        }
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options)
        : DbContext(options), IModuleDbContext
    {
        public DbSet<TestEntity> Items => Set<TestEntity>();
        public DbContext Instance => this;
    }
}
