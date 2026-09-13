using AllSpice.CleanModularMonolith.ApiContracts.Ledger.Responses;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Ledger.Api.Endpoints.Accounts;

/// <summary>The account's full event stream with store metadata — the audit trail.</summary>
public sealed class GetAccountHistoryEndpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<AccountHistoryEntryResponse>>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/api/ledger/accounts/{accountId:guid}/history");
        Policies(PermissionPolicy.For("ledger:accounts.read"));
        Tags("Ledger");
        Summary(s => s.Summary = "The account's full event stream with store metadata — the audit trail.");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountHistoryQuery(Route<Guid>("accountId")), ct);
        if (result.IsSuccess)
        {
            IReadOnlyList<AccountHistoryEntryResponse> entries = result.Value
                .Select(e => new AccountHistoryEntryResponse(e.Version, e.EventType, e.Timestamp, e.CorrelationId, e.IdempotencyKey, e.Data))
                .ToList();
            await TypedResults.Ok(entries).ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
