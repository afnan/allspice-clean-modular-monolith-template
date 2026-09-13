using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.WithdrawFunds;

public sealed class WithdrawFundsCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<WithdrawFundsCommand, Result>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(WithdrawFundsCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.LoadAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound($"Account {request.AccountId} was not found.");
        }

        account.Withdraw(Money.Of(request.Amount, account.Currency), request.Reference, _timeProvider.GetUtcNow());
        await _accounts.SaveAsync(account, cancellationToken);
        return Result.Success();
    }
}
