using FluentValidation;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.InvitedByUserId)
            .NotEmpty();
    }
}
