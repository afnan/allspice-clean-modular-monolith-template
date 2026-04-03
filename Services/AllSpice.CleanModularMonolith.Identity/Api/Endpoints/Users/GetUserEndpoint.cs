using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.GetUser;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Users;

public sealed class GetUserEndpoint : EndpointWithoutRequest<Results<Ok<UserDto>, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public GetUserEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/identity/users/{externalId}");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Gets a user by their external (Keycloak) ID.";
        });
    }

    public override async Task<Results<Ok<UserDto>, NotFound<IdentityErrorResponse>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var externalId = Route<string>("externalId") ?? string.Empty;
        var result = await _mediator.Send(new GetUserQuery(externalId), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(result.Value),
            ResultStatus.NotFound => TypedResults.NotFound(new IdentityErrorResponse(result.Errors.ToArray())),
            _ => result.ToProblem()
        };
    }
}
