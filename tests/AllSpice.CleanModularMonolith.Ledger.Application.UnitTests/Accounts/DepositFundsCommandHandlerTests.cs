using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Ledger.Application.UnitTests.Accounts;

public class DepositFundsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private readonly Mock<IAccountRepository> _repository = new();
    private readonly DepositFundsCommandHandler _handler;

    public DepositFundsCommandHandlerTests()
    {
        _handler = new DepositFundsCommandHandler(_repository.Object, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Deposits_into_a_loaded_account_and_saves_it()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), Currency.Aud, Now);
        _repository.Setup(r => r.LoadAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var result = await _handler.Handle(new DepositFundsCommand(account.Id, 25m, "INV-9"), CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(25m, account.Balance.Amount);
        _repository.Verify(r => r.SaveAsync(account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_NotFound_when_the_account_does_not_exist()
    {
        _repository.Setup(r => r.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        var result = await _handler.Handle(new DepositFundsCommand(Guid.NewGuid(), 1m, "x"), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        _repository.Verify(r => r.SaveAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
