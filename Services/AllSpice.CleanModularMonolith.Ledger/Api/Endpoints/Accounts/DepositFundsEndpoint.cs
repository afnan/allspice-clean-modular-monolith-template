using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Deposits funds into a ledger account.</summary>
public sealed class DepositFundsEndpoint(IMediator mediator) : Endpoint<MoneyMovementRequest>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/api/ledger/accounts/{accountId:guid}/deposits");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(MoneyMovementRequest req, CancellationToken ct)
    {
        var accountId = Route<Guid>("accountId");
        var result = await _mediator.Send(new DepositFundsCommand(accountId, req.Amount, req.Reference), ct);
        if (result.IsSuccess)
        {
            await TypedResults.NoContent().ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
