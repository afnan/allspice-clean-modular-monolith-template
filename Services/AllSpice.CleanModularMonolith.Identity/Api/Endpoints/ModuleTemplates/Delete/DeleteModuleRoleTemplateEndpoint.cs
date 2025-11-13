using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.DeleteModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Delete;

public sealed class DeleteModuleRoleTemplateEndpoint : EndpointWithoutRequest<Results<NoContent, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public DeleteModuleRoleTemplateEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/identity/module-templates/{templateKey}");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Deletes a module role template.";
        });
    }

    public override async Task<Results<NoContent, NotFound<IdentityErrorResponse>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var templateKey = Route<string>("templateKey") ?? string.Empty;
        var result = await _mediator.Send(new DeleteModuleRoleTemplateCommand(templateKey), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.NoContent(),
            ResultStatus.NotFound => TypedResults.NotFound(new IdentityErrorResponse(result.Errors.ToArray())),
            _ => result.ToProblem()
        };
    }
}


