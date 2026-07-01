using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.CreatePermission;

public sealed class CreatePermissionCommandValidator : AbstractValidator<CreatePermissionCommand>
{
    public CreatePermissionCommandValidator()
    {
        RuleFor(c => c.Key)
            .Must(PermissionKey.IsValid)
            .WithMessage(
                "Permission key is invalid. Keys must be lowercase dot- or colon-namespaced segments, " +
                "e.g. 'cms.publish' or 'cms:articles.publish'.");
    }
}
