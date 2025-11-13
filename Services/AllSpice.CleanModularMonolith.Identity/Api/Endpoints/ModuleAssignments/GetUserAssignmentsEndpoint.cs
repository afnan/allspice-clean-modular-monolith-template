using AllSpice.CleanModularMonolith.Identity.Api.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Queries.GetUserAssignments;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

/// <summary>
/// API endpoint that returns active module-role assignments for a user.
/// </summary>
public sealed class GetUserAssignmentsEndpoint : EndpointWithoutRequest<IReadOnlyCollection<ModuleRoleAssignmentResponse>>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserAssignmentsEndpoint"/> class.
    /// </summary>
    /// <param name="mediator">Mediator used to dispatch queries.</param>
    public GetUserAssignmentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/api/identity/module-assignments/{userId}");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Returns the active module role assignments for a user.";
        });
    }

    /// <inheritdoc />
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


