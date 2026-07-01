using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.CreatePermission;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using AuthzPermissions = AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.Permissions;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class CreatePermissionEndpoint(IMediator mediator)
    : Endpoint<CreatePermissionRequest, Results<Created, ProblemHttpResult>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Post("/api/identity/authz/permissions");
        Policies(PermissionPolicy.For(AuthzPermissions.AuthzManage));
        Tags("Authorization");
        Summary(summary =>
        {
            summary.Summary = "Creates a new non-system permission. Bumps the authz map version.";
        });
    }

    public override async Task<Results<Created, ProblemHttpResult>> ExecuteAsync(
        CreatePermissionRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePermissionCommand(req.Key, req.Description), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Created(),
            ResultStatus.Invalid => result.ToProblem(StatusCodes.Status400BadRequest),
            _ => result.ToProblem()
        };
    }
}

