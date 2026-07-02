using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Behaviors;

/// <summary>
/// A command handler can signal failure two ways: by THROWING (the transaction is never opened, so nothing
/// persists) or by RETURNING a failure Ardalis <see cref="Result"/>. TransactionBehavior must honour the same
/// "failure => no state change" invariant for both — a handler that stages writes and then returns a failure
/// Result must NOT have those writes committed. Uses a real SQLite-backed context because the behavior opens
/// an actual DB transaction (EF InMemory can't).
/// </summary>
public sealed class TransactionBehaviorFailureResultTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestModuleDbContext _db;

    public TransactionBehaviorFailureResultTests()
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
    public async Task Commits_staged_writes_when_the_handler_returns_success()
    {
        var behavior = CreateBehavior();

        var result = await behavior.Handle(
            new FakeCommand(), StageRowThenReturn("ok-row", Result.Success()), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        await using var fresh = new TestModuleDbContext(Options());
        Assert.True(await fresh.Items.AnyAsync(i => i.Name == "ok-row"),
            "a successful command must commit its staged writes");
    }

    [Fact]
    public async Task Does_not_commit_staged_writes_when_the_handler_returns_conflict()
    {
        var behavior = CreateBehavior();

        var result = await behavior.Handle(
            new FakeCommand(), StageRowThenReturn("conflict-row", Result.Conflict()), CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        await using var fresh = new TestModuleDbContext(Options());
        Assert.False(await fresh.Items.AnyAsync(i => i.Name == "conflict-row"),
            "a Conflict result must not commit staged writes");
    }

    [Fact]
    public async Task Does_not_commit_staged_writes_when_the_handler_returns_invalid()
    {
        var behavior = CreateBehavior();

        var result = await behavior.Handle(
            new FakeCommand(), StageRowThenReturn("invalid-row", Result.Invalid()), CancellationToken.None);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        await using var fresh = new TestModuleDbContext(Options());
        Assert.False(await fresh.Items.AnyAsync(i => i.Name == "invalid-row"),
            "an Invalid result must not commit staged writes");
    }

    [Fact]
    public async Task Clears_the_change_tracker_when_the_handler_returns_a_failure_result()
    {
        var behavior = CreateBehavior();

        await behavior.Handle(
            new FakeCommand(), StageRowThenReturn("leaked-row", Result.Conflict()), CancellationToken.None);

        // The staged-but-discarded entity must not remain tracked on the scoped context. Otherwise a
        // SUBSEQUENT ITransactional command sharing the same scope would see it as dirty and commit it.
        Assert.Empty(_db.ChangeTracker.Entries());
    }

    private TransactionBehavior<FakeCommand, Result> CreateBehavior() =>
        new([_db], Mock.Of<IDomainEventDispatcher>(), [], new PostCommitActions(),
            NullLogger<TransactionBehavior<FakeCommand, Result>>.Instance);

    private MessageHandlerDelegate<FakeCommand, Result> StageRowThenReturn(string name, Result result) =>
        (_, _) =>
        {
            _db.Items.Add(new TestEntity { Name = name }); // stage only — the behavior owns SaveChanges + commit
            return ValueTask.FromResult(result);
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
