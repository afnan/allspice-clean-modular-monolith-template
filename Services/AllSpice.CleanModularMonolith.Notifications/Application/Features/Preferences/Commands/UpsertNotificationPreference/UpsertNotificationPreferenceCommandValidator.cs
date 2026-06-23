using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;

public sealed class UpsertNotificationPreferenceCommandValidator : AbstractValidator<UpsertNotificationPreferenceCommand>
{
    public UpsertNotificationPreferenceCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId must be a non-empty local user UUID.");
    }
}
