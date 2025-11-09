using FluentValidation;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Commands.AssignModuleRole;

public sealed class AssignModuleRoleCommandValidator : AbstractValidator<AssignModuleRoleCommand>
{
    public AssignModuleRoleCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(command => command.ModuleKey)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(command => command.RoleKey)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(command => command.AssignedBy)
            .NotEmpty()
            .MaximumLength(128);
    }
}


