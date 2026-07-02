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

        // Description maps to a HasMaxLength(500) column (PermissionConfiguration) and Permission.Create
        // does not guard length, so a >500-char value would surface as a DbUpdateException (HTTP 500)
        // instead of a clean 400. Description is optional (Permission.Create tolerates null → ""), so only
        // the upper bound is enforced here.
        RuleFor(c => c.Description)
            .MaximumLength(500)
            .WithMessage("Permission description must be 500 characters or fewer.");
    }
}
