using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;
using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.ApplyModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Apply;

public sealed class ApplyModuleRoleTemplateEndpoint : Endpoint<ApplyModuleRoleTemplateRequest, Results<Ok<IReadOnlyCollection<ModuleRoleAssignmentResponse>>, ValidationProblem, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public ApplyModuleRoleTemplateEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/identity/module-templates/{templateKey}/apply");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Applies a module role template to a user.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyCollection<ModuleRoleAssignmentResponse>>, ValidationProblem, NotFound<IdentityErrorResponse>, ProblemHttpResult>> ExecuteAsync(
        ApplyModuleRoleTemplateRequest req,
        CancellationToken ct)
    {
        var templateKey = Route<string>("templateKey") ?? string.Empty;

        var assignedBy = User.GetUserObjectId();
        if (string.IsNullOrWhiteSpace(assignedBy))
        {
            return User.ToUnauthorizedProblem();
        }

        var result = await _mediator.Send(
            new ApplyModuleRoleTemplateCommand(templateKey, req.UserId, assignedBy),
            ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok<IReadOnlyCollection<ModuleRoleAssignmentResponse>>(result.Value
                .Select(dto => new ModuleRoleAssignmentResponse(
                    dto.AssignmentId,
                    dto.UserId,
                    dto.ModuleKey,
                    dto.RoleKey,
                    dto.AssignedUtc,
                    dto.RevokedUtc))
                .ToList()),
            ResultStatus.Invalid => result.ToValidationProblem(),
            ResultStatus.NotFound => TypedResults.NotFound(new IdentityErrorResponse(result.Errors.ToArray())),
            _ => result.ToProblem()
        };
    }
}


