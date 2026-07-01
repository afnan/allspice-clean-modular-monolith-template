using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.DeletePermission;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using AuthzPermissions = AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.Permissions;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class DeletePermissionEndpoint(IMediator mediator)
    : EndpointWithoutRequest<Results<NoContent, ProblemHttpResult>>
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Delete("/api/identity/authz/permissions/{id}");
        Policies(PermissionPolicy.For(AuthzPermissions.AuthzManage));
        Tags("Authorization");
        Summary(summary =>
        {
            summary.Summary = "Deletes a non-system permission. System permissions are deletion-protected.";
        });
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _mediator.Send(new DeletePermissionCommand(id), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.NoContent(),
            ResultStatus.NotFound => result.ToProblem(StatusCodes.Status404NotFound),
            ResultStatus.Forbidden => result.ToProblem(StatusCodes.Status403Forbidden),
            _ => result.ToProblem()
        };
    }
}
