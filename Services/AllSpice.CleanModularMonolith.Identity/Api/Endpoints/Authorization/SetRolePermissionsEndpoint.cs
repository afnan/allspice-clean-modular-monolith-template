using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.SetRolePermissions;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using AuthzPermissions = AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.Permissions;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class SetRolePermissionsEndpoint(IMediator mediator)
    : Endpoint<SetRolePermissionsRequest, Results<NoContent, ProblemHttpResult>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Put("/api/identity/authz/roles/{key}/permissions");
        Policies(PermissionPolicy.For(AuthzPermissions.AuthzManage));
        Tags("Authorization");
        Summary(summary =>
        {
            summary.Summary = "Replaces all permissions assigned to a role. Unknown permission keys are rejected.";
        });
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        SetRolePermissionsRequest req, CancellationToken ct)
    {
        var key = Route<string>("key") ?? string.Empty;
        var result = await _mediator.Send(new SetRolePermissionsCommand(key, req.PermissionKeys), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.NoContent(),
            ResultStatus.NotFound => result.ToProblem(StatusCodes.Status404NotFound),
            ResultStatus.Invalid => result.ToProblem(StatusCodes.Status400BadRequest),
            _ => result.ToProblem()
        };
    }
}

