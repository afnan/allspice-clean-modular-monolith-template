using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.WithdrawFunds;

public sealed class WithdrawFundsCommandValidator : AbstractValidator<WithdrawFundsCommand>
{
    public WithdrawFundsCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}
