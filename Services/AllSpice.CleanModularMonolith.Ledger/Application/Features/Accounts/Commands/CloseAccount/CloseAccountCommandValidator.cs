using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.CloseAccount;

public sealed class CloseAccountCommandValidator : AbstractValidator<CloseAccountCommand>
{
    public CloseAccountCommandValidator() => RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
}
