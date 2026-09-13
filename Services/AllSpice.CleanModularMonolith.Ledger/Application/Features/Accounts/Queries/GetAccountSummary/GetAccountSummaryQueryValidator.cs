using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;

public sealed class GetAccountSummaryQueryValidator : AbstractValidator<GetAccountSummaryQuery>
{
    public GetAccountSummaryQueryValidator() => RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
}
