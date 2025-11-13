using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.UpdateModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Update;

public sealed class UpdateModuleRoleTemplateEndpoint : Endpoint<UpdateModuleRoleTemplateRequest, Results<Ok<UpdateModuleRoleTemplateResponse>, ValidationProblem, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public UpdateModuleRoleTemplateEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/identity/module-templates/{templateKey}");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Updates an existing module role template.";
        });
    }

    public override async Task<Results<Ok<UpdateModuleRoleTemplateResponse>, ValidationProblem, NotFound<IdentityErrorResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateModuleRoleTemplateRequest req,
        CancellationToken ct)
    {
        var templateKey = Route<string>("templateKey") ?? string.Empty;

        var command = new UpdateModuleRoleTemplateCommand(
            templateKey,
            req.Name,
            req.Description,
            req.Roles.Select(role => new ModuleRoleTemplateRoleDto(role.ModuleKey, role.RoleKey)).ToList());

        var result = await _mediator.Send(command, ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(UpdateModuleRoleTemplateResponse.FromDto(result.Value)),
            ResultStatus.Invalid => result.ToValidationProblem(),
            ResultStatus.NotFound => TypedResults.NotFound(new IdentityErrorResponse(result.Errors.ToArray())),
            _ => result.ToProblem()
        };
    }
}


