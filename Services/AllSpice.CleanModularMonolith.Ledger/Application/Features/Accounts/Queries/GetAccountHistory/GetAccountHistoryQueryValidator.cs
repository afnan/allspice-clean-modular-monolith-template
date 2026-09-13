using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;

public sealed class GetAccountHistoryQueryValidator : AbstractValidator<GetAccountHistoryQuery>
{
    public GetAccountHistoryQueryValidator() => RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
}
