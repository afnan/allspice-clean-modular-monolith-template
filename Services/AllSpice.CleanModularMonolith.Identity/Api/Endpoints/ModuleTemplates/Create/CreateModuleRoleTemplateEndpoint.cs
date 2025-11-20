using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Web;
using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.CreateModuleRoleTemplate;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Create;

public sealed class CreateModuleRoleTemplateEndpoint : Endpoint<CreateModuleRoleTemplateRequest, Results<Created<CreateModuleRoleTemplateResponse>, ValidationProblem, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public CreateModuleRoleTemplateEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/identity/module-templates");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Creates a new module role template.";
        });
    }

    public override async Task<Results<Created<CreateModuleRoleTemplateResponse>, ValidationProblem, ProblemHttpResult>> ExecuteAsync(
        CreateModuleRoleTemplateRequest req,
        CancellationToken ct)
    {
        var command = new CreateModuleRoleTemplateCommand(
            req.TemplateKey,
            req.Name,
            req.Description,
            req.Roles.Select(role => new ModuleRoleTemplateRoleDto(role.ModuleKey, role.RoleKey)).ToList());

        var result = await _mediator.Send(command, ct);

        return result.Status switch
        {
            ResultStatus.Ok or ResultStatus.Created => TypedResults.Created(
                $"/api/identity/module-templates/{result.Value.TemplateKey}",
                CreateModuleRoleTemplateResponse.FromDto(result.Value)),
            ResultStatus.Invalid => result.ToValidationProblem(),
            ResultStatus.Conflict => result.ToProblem(StatusCodes.Status409Conflict),
            _ => result.ToProblem()
        };
    }
}


