using AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Opens an event-sourced ledger account (reference implementation, ADR-0009).</summary>
public sealed class OpenAccountEndpoint(IMediator mediator) : Endpoint<OpenAccountRequest, OpenAccountResponse>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/api/ledger/accounts");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
        Summary(s => s.Summary = "Opens an event-sourced ledger account (reference implementation, ADR-0009).");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(OpenAccountRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new OpenAccountCommand(req.OwnerUserId, req.Currency), ct);
        if (result.IsSuccess)
        {
            await TypedResults.Created($"/api/ledger/accounts/{result.Value}", new OpenAccountResponse(result.Value)).ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
