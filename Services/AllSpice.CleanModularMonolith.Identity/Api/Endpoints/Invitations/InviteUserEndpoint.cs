using AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Invitations;

public sealed class InviteUserEndpoint : Endpoint<InviteUserRequest, Results<Created<Guid>, ValidationProblem, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public InviteUserEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/identity/invitations");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Invites a new user by creating a Keycloak account with a temporary password and sending an invitation notification.";
        });
    }

    public override async Task<Results<Created<Guid>, ValidationProblem, ProblemHttpResult>> ExecuteAsync(
        InviteUserRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;

        var command = new InviteUserCommand(
            req.Email,
            req.FirstName,
            req.LastName,
            req.Role,
            userId);

        var result = await _mediator.Send(command, ct);

        return result.Status switch
        {
            ResultStatus.Ok or ResultStatus.Created => TypedResults.Created(
                $"/api/identity/invitations/{result.Value}", result.Value),
            ResultStatus.Invalid => result.ToValidationProblem(),
            ResultStatus.Conflict => result.ToProblem(StatusCodes.Status409Conflict),
            _ => result.ToProblem()
        };
    }
}
