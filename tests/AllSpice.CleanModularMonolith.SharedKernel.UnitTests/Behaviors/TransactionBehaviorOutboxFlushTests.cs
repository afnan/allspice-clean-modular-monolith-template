using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Behaviors;

/// <summary>
/// TransactionBehavior must release the durable outbox right after it commits, so integration events are
/// sent promptly instead of waiting for the messaging recovery sweep — but only after a real commit, and
/// never at the cost of failing an already-committed command if the flush itself fails. Uses a real
/// SQLite-backed context because the behavior opens an actual DB transaction (EF InMemory can't).
/// </summary>
public sealed class TransactionBehaviorOutboxFlushTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestModuleDbContext _db;

    public TransactionBehaviorOutboxFlushTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // keep open: the in-memory database lives only as long as the connection
        _db = new TestModuleDbContext(Options());
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Flushes_the_outbox_once_after_a_successful_commit()
    {
        var flusher = new Mock<IOutboxFlusher>();
        var behavior = CreateBehavior(flusher.Object);

        var result = await behavior.Handle(new FakeCommand(), StageARow("committed"), CancellationToken.None);

        Assert.Equal("ok", result);
        flusher.Verify(f => f.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);

        await using var fresh = new TestModuleDbContext(Options());
        Assert.True(await fresh.Items.AnyAsync(i => i.Name == "committed"), "row was not committed");
    }

    [Fact]
    public async Task Does_not_flush_when_the_handler_stages_nothing()
    {
        var flusher = new Mock<IOutboxFlusher>();
        var behavior = CreateBehavior(flusher.Object);

        await behavior.Handle(new FakeCommand(), (_, _) => ValueTask.FromResult("ok"), CancellationToken.None);

        flusher.Verify(f => f.FlushAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_flush_failure_does_not_fail_the_already_committed_command()
    {
        var flusher = new Mock<IOutboxFlusher>();
        flusher.Setup(f => f.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException(new InvalidOperationException("message broker unreachable")));
        var behavior = CreateBehavior(flusher.Object);

        // The command must still succeed and its data stay committed — the durable envelope is already
        // persisted, so a failed flush only means delivery falls back to the recovery loop.
        var result = await behavior.Handle(new FakeCommand(), StageARow("still-committed"), CancellationToken.None);

        Assert.Equal("ok", result);
        await using var fresh = new TestModuleDbContext(Options());
        Assert.True(await fresh.Items.AnyAsync(i => i.Name == "still-committed"), "row was not committed");
    }

    private TransactionBehavior<FakeCommand, string> CreateBehavior(IOutboxFlusher flusher) =>
        new([_db], Mock.Of<IDomainEventDispatcher>(), [flusher], new PostCommitActions(),
            NullLogger<TransactionBehavior<FakeCommand, string>>.Instance);

    private MessageHandlerDelegate<FakeCommand, string> StageARow(string name) =>
        (_, _) =>
        {
            _db.Items.Add(new TestEntity { Name = name }); // stage only — the behavior owns SaveChanges + commit
            return ValueTask.FromResult("ok");
        };

    private DbContextOptions<TestModuleDbContext> Options() =>
        new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite(_connection).Options;

    private sealed record FakeCommand : IMessage, ITransactional;

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
