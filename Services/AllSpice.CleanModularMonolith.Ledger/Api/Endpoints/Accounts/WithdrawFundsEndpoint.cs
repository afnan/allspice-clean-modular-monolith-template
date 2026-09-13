using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.WithdrawFunds;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Withdraws funds from a ledger account.</summary>
public sealed class WithdrawFundsEndpoint(IMediator mediator) : Endpoint<MoneyMovementRequest>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/api/ledger/accounts/{accountId:guid}/withdrawals");
        Policies(PermissionPolicy.For("ledger:accounts.write"));
        Tags("Ledger");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(MoneyMovementRequest req, CancellationToken ct)
    {
        var accountId = Route<Guid>("accountId");
        var result = await _mediator.Send(new WithdrawFundsCommand(accountId, req.Amount, req.Reference), ct);
        if (result.IsSuccess)
        {
            await TypedResults.NoContent().ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
