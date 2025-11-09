using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Commands.AssignModuleRole;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

public sealed class AssignModuleRoleEndpoint : Endpoint<AssignModuleRoleRequest, ModuleRoleAssignmentResponse>
{
    private readonly IMediator _mediator;

    public AssignModuleRoleEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/identity/module-assignments");
        Roles("Identity.Admin");
        Summary(summary =>
        {
            summary.Summary = "Assigns a module role to a user.";
            summary.Description = "Creates or updates a module role assignment for a given Authentik user.";
        });
    }

    public override async Task HandleAsync(AssignModuleRoleRequest req, CancellationToken ct)
    {
        var command = new AssignModuleRoleCommand(req.UserId, req.ModuleKey, req.RoleKey, req.AssignedBy);
        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new IdentityErrorResponse(result.Errors.ToArray()), cancellationToken: ct);
            return;
        }

        var response = new ModuleRoleAssignmentResponse(
            result.Value.AssignmentId,
            result.Value.UserId,
            result.Value.ModuleKey,
            result.Value.RoleKey,
            result.Value.AssignedUtc,
            result.Value.RevokedUtc);

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}


