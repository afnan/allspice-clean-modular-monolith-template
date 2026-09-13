using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;

public sealed class DepositFundsCommandValidator : AbstractValidator<DepositFundsCommand>
{
    public DepositFundsCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}
