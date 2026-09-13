using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class OpenAccountCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private readonly Mock<IAccountRepository> _repository = new();
    private readonly OpenAccountCommandHandler _handler;

    public OpenAccountCommandHandlerTests()
    {
        _handler = new OpenAccountCommandHandler(_repository.Object, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Adds_a_new_account_and_returns_its_id()
    {
        Account? added = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback((Account a, CancellationToken _) => added = a)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new OpenAccountCommand(Guid.NewGuid(), "aud"), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.NotNull(added);
        Assert.Equal(added.Id, result.Value);
        Assert.Equal("AUD", added.Currency.Code);
        Assert.Equal(Now, added.OpenedUtc);
    }
}
