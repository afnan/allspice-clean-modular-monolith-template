using FluentValidation;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Queries.GetUserAssignments;

public sealed class GetUserAssignmentsQueryValidator : AbstractValidator<GetUserAssignmentsQuery>
{
    public GetUserAssignmentsQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty()
            .MaximumLength(128);
    }
}


