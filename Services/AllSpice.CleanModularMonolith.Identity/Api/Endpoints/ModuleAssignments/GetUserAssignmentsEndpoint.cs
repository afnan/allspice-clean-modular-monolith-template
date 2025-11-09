using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Queries.GetUserAssignments;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

public sealed class GetUserAssignmentsEndpoint : EndpointWithoutRequest<IReadOnlyCollection<ModuleRoleAssignmentResponse>>
{
    private readonly IMediator _mediator;

    public GetUserAssignmentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/identity/module-assignments/{userId}");
        Roles("Identity.Admin");
        Summary(summary =>
        {
            summary.Summary = "Returns the active module role assignments for a user.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<string>("userId") ?? string.Empty;
        var result = await _mediator.Send(new GetUserAssignmentsQuery(userId), ct);

        if (!result.IsSuccess)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new IdentityErrorResponse(result.Errors.ToArray()), cancellationToken: ct);
            return;
        }

        var response = result.Value
            .Select(dto => new ModuleRoleAssignmentResponse(
                dto.AssignmentId,
                dto.UserId,
                dto.ModuleKey,
                dto.RoleKey,
                dto.AssignedUtc,
                dto.RevokedUtc))
            .ToList();

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}


