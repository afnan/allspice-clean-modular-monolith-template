using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Queries.ListModuleRoleTemplates;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.List;

public sealed class ListModuleRoleTemplatesEndpoint : EndpointWithoutRequest<Results<Ok<IReadOnlyCollection<ListModuleRoleTemplateResponse>>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public ListModuleRoleTemplatesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/identity/module-templates");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Lists all module role templates.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyCollection<ListModuleRoleTemplateResponse>>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListModuleRoleTemplatesQuery(), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok<IReadOnlyCollection<ListModuleRoleTemplateResponse>>(
                result.Value.Select(ListModuleRoleTemplateResponse.FromDto).ToList()),
            _ => result.ToProblem()
        };
    }
}


