using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Queries.GetModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Get;

public sealed class GetModuleRoleTemplateEndpoint : EndpointWithoutRequest<Results<Ok<GetModuleRoleTemplateResponse>, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public GetModuleRoleTemplateEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/identity/module-templates/{templateKey}");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Gets a module role template by key.";
        });
    }

    public override async Task<Results<Ok<GetModuleRoleTemplateResponse>, NotFound<IdentityErrorResponse>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var templateKey = Route<string>("templateKey") ?? string.Empty;
        var result = await _mediator.Send(new GetModuleRoleTemplateQuery(templateKey), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(GetModuleRoleTemplateResponse.FromDto(result.Value)),
            ResultStatus.NotFound => TypedResults.NotFound(new IdentityErrorResponse(result.Errors.ToArray())),
            _ => result.ToProblem()
        };
    }
}


