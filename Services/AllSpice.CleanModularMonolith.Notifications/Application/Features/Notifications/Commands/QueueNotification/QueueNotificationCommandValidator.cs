using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
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

        // SMS has no registered channel handler yet (only Email and InApp are wired). Reject it up-front with a
        // clear message; otherwise a queued Sms notification is accepted, then fails every delivery attempt with
        // "No notification channel registered for 'Sms'" and exhausts its attempt budget. Remove this rule when
        // an SMS provider is added.
        RuleFor(x => x.Channel)
            .Must(channel => channel != NotificationChannel.Sms)
            .WithMessage("SMS channel is not yet supported.");

        // Channel-conditional recipient rules: a channel can only deliver through the matching contact method,
        // and a malformed recipient would otherwise be accepted and burn all delivery attempts. Validate the
        // contact method the selected channel actually uses.
        When(x => x.Channel == NotificationChannel.Email, () =>
        {
            RuleFor(x => x.RecipientEmail)
                .NotEmpty()
                .WithMessage("RecipientEmail is required for Email notifications.")
                .EmailAddress()
                .WithMessage("RecipientEmail must be a valid email address.");
        });

        When(x => x.Channel == NotificationChannel.Sms, () =>
        {
            RuleFor(x => x.RecipientPhoneNumber)
                .NotEmpty()
                .WithMessage("RecipientPhoneNumber is required for SMS notifications.");
        });

        RuleFor(x => x)
            .Must(command => !string.IsNullOrWhiteSpace(command.RecipientEmail) || !string.IsNullOrWhiteSpace(command.RecipientPhoneNumber))
            .WithMessage("At least an email address or phone number must be provided.");
    }
}


