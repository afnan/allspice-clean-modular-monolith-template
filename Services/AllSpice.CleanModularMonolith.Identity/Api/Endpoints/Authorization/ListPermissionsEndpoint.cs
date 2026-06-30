using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListPermissions;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using AuthzPermissions = AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.Permissions;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class ListPermissionsEndpoint(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<IReadOnlyList<PermissionDto>>, ProblemHttpResult>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Get("/api/identity/authz/permissions");
        Policies(PermissionPolicy.For(AuthzPermissions.AuthzRead));
        Tags("Authorization");
        Summary(summary =>
        {
            summary.Summary = "Lists all permissions ordered by key.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyList<PermissionDto>>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ListPermissionsQuery(), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(result.Value),
            _ => result.ToProblem()
        };
    }
}
