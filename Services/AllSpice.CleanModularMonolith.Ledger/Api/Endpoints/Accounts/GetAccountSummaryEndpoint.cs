using AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>Returns the account's current state, as materialised by the inline projection.</summary>
public sealed class GetAccountSummaryEndpoint(IMediator mediator) : EndpointWithoutRequest<AccountSummaryResponse>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/api/ledger/accounts/{accountId:guid}");
        Policies(PermissionPolicy.For("ledger:accounts.read"));
        Tags("Ledger");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountSummaryQuery(Route<Guid>("accountId")), ct);
        if (result.IsSuccess)
        {
            var s = result.Value;
            await TypedResults.Ok(new AccountSummaryResponse(s.AccountId, s.OwnerUserId, s.Currency, s.Balance, s.Status, s.Version, s.LastActivityUtc))
                .ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
