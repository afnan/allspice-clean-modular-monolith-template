using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;

public sealed class GetAccountSummaryQueryHandler(IAccountRepository accounts)
    : IRequestHandler<GetAccountSummaryQuery, Result<AccountSummaryDto>>
{
    private readonly IAccountRepository _accounts = accounts;

    public async ValueTask<Result<AccountSummaryDto>> Handle(GetAccountSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _accounts.GetSummaryAsync(request.AccountId, cancellationToken);
        return summary is null
            ? Result<AccountSummaryDto>.NotFound($"Account {request.AccountId} was not found.")
            : Result.Success(summary);
    }
}
