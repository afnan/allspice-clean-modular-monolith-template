using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;

public sealed class QueueNotificationCommandValidator : AbstractValidator<QueueNotificationCommand>
{
    public QueueNotificationCommandValidator()
    {
        // RecipientUserId is optional: a notification can target a "userless" recipient identified
        // only by a contact method (e.g. an invitation email to someone with no local account yet).
        // When present it must be the recipient's canonical local UUID.
        When(x => !string.IsNullOrWhiteSpace(x.RecipientUserId), () =>
        {
            RuleFor(x => x.RecipientUserId)
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("RecipientUserId must be a valid GUID when provided.");
        });

        When(x => string.IsNullOrWhiteSpace(x.TemplateKey), () =>
        {
            RuleFor(x => x.Subject)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Body)
                .NotEmpty();
        });

        RuleFor(x => x.Subject)
            .MaximumLength(200);

        RuleFor(x => x)
            .Must(command => !string.IsNullOrWhiteSpace(command.RecipientEmail) || !string.IsNullOrWhiteSpace(command.RecipientPhoneNumber))
            .WithMessage("At least an email address or phone number must be provided.");
    }
}


