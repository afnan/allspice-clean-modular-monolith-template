using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;

public sealed class DepositFundsCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<DepositFundsCommand, Result>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(DepositFundsCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.LoadAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound($"Account {request.AccountId} was not found.");
        }

        // Money is denominated in the account's own currency; a mismatch is impossible from this API by design.
        account.Deposit(Money.Of(request.Amount, account.Currency), request.Reference, _timeProvider.GetUtcNow());
        await _accounts.SaveAsync(account, cancellationToken);
        return Result.Success();
    }
}
