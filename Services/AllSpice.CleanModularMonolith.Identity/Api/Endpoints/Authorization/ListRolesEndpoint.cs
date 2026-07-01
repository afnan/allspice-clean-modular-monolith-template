using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListRoles;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using AuthzPermissions = AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.Permissions;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class ListRolesEndpoint(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<IReadOnlyList<RoleDto>>, ProblemHttpResult>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Get("/api/identity/authz/roles");
        Policies(PermissionPolicy.For(AuthzPermissions.AuthzRead));
        Tags("Authorization");
        Summary(summary =>
        {
            summary.Summary = "Lists all roles ordered by key.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyList<RoleDto>>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ListRolesQuery(), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(result.Value),
            _ => result.ToProblem()
        };
    }
}
