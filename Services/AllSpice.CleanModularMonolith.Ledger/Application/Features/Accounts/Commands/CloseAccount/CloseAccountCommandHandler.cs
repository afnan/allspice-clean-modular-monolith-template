using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.CloseAccount;

public sealed class CloseAccountCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<CloseAccountCommand, Result>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.LoadAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound($"Account {request.AccountId} was not found.");
        }

        account.Close(_timeProvider.GetUtcNow());
        await _accounts.SaveAsync(account, cancellationToken); // archives the stream (MarkForArchive)
        return Result.Success();
    }
}
