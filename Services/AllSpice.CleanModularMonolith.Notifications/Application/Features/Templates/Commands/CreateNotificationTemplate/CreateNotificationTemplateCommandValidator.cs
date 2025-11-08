using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Templates.Commands.CreateNotificationTemplate;

public sealed class CreateNotificationTemplateCommandValidator : AbstractValidator<CreateNotificationTemplateCommand>
{
    public CreateNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.SubjectTemplate)
            .NotEmpty();

        RuleFor(x => x.BodyTemplate)
            .NotEmpty();
    }
}


