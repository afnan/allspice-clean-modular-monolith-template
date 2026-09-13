using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;

public sealed class OpenAccountCommandHandler(IAccountRepository accounts, TimeProvider timeProvider)
    : IRequestHandler<OpenAccountCommand, Result<Guid>>
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result<Guid>> Handle(OpenAccountCommand request, CancellationToken cancellationToken)
    {
        var account = Account.Open(Guid.NewGuid(), request.OwnerUserId, Currency.FromCode(request.Currency), _timeProvider.GetUtcNow());
        await _accounts.AddAsync(account, cancellationToken);
        return Result.Success(account.Id);
    }
}
