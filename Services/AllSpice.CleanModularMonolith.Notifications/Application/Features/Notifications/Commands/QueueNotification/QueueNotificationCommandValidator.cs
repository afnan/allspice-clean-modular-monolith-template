using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;

public sealed class QueueNotificationCommandValidator : AbstractValidator<QueueNotificationCommand>
{
    public QueueNotificationCommandValidator()
    {
        RuleFor(x => x.RecipientUserId)
            .NotEmpty();

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


