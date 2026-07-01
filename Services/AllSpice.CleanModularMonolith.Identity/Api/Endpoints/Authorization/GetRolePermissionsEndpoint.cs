using AllSpice.CleanModularMonolith.ApiContracts.Identity.Responses;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.GetRolePermissions;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using AuthzPermissions = AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.Permissions;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class GetRolePermissionsEndpoint(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<IReadOnlyList<string>>, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Get("/api/identity/authz/roles/{key}/permissions");
        Policies(PermissionPolicy.For(AuthzPermissions.AuthzRead));
        Tags("Authorization");
        Summary(summary =>
        {
            summary.Summary = "Gets the permission keys assigned to a role.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyList<string>>, NotFound<IdentityErrorResponse>, ProblemHttpResult>>
        ExecuteAsync(CancellationToken ct)
    {
        var key = Route<string>("key") ?? string.Empty;
        var result = await _mediator.Send(new GetRolePermissionsQuery(key), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(result.Value),
            ResultStatus.NotFound => TypedResults.NotFound(new IdentityErrorResponse(result.Errors.ToArray())),
            _ => result.ToProblem()
        };
    }
}
