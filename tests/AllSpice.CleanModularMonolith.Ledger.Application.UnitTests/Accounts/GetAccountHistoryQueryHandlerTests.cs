using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class GetAccountHistoryQueryHandlerTests
{
    [Fact]
    public async Task Maps_stored_events_including_the_idempotency_header()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IAccountRepository>();
        repository.Setup(r => r.HistoryAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new StoredEvent(1, 10, DateTimeOffset.UnixEpoch, "account_opened", "corr-1",
                new Dictionary<string, object?> { ["idempotency-key"] = "key-1" },
                new AccountOpened(id, Guid.NewGuid(), "AUD", DateTimeOffset.UnixEpoch)),
        ]);
        var handler = new GetAccountHistoryQueryHandler(repository.Object);

        var result = await handler.Handle(new GetAccountHistoryQuery(id), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        var entry = Assert.Single(result.Value);
        Assert.Equal(1, entry.Version);
        Assert.Equal("account_opened", entry.EventType);
        Assert.Equal("corr-1", entry.CorrelationId);
        Assert.Equal("key-1", entry.IdempotencyKey);
        Assert.IsType<AccountOpened>(entry.Data);
    }

    [Fact]
    public async Task Returns_NotFound_for_an_empty_stream()
    {
        var repository = new Mock<IAccountRepository>();
        repository.Setup(r => r.HistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var handler = new GetAccountHistoryQueryHandler(repository.Object);

        var result = await handler.Handle(new GetAccountHistoryQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
