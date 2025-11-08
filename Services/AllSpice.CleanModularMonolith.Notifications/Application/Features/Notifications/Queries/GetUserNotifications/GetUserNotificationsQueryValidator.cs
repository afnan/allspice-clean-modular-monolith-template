using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed class GetUserNotificationsQueryValidator : AbstractValidator<GetUserNotificationsQuery>
{
    public GetUserNotificationsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}


