using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;

public sealed class UpsertNotificationPreferenceCommandValidator : AbstractValidator<UpsertNotificationPreferenceCommand>
{
    public UpsertNotificationPreferenceCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}


