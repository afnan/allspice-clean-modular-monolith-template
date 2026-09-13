using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.CloseAccount;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Closes a ledger account.</summary>
public sealed class CloseAccountEndpoint(IMediator mediator) : EndpointWithoutRequest
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/api/ledger/accounts/{accountId:guid}/close");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseAccountCommand(Route<Guid>("accountId")), ct);
        if (result.IsSuccess)
        {
            await TypedResults.NoContent().ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
