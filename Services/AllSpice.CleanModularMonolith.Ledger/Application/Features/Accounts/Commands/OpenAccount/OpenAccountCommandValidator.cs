using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;

public sealed class OpenAccountCommandValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEqual(Guid.Empty).WithMessage("OwnerUserId must be a non-empty local user UUID.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(code => Currency.TryFromCode(code, out _))
            .WithMessage(_ => $"Unsupported currency. Supported: {string.Join(", ", Currency.List.Select(c => c.Code))}.");
    }
}
