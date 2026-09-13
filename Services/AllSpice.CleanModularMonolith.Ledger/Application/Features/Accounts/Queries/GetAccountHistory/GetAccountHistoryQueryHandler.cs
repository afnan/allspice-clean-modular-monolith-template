using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;

/// <summary>The audit-trail read: the raw stream with store metadata. Lists of accounts must use a projection instead.</summary>
public sealed class GetAccountHistoryQueryHandler(IAccountRepository accounts)
    : IRequestHandler<GetAccountHistoryQuery, Result<IReadOnlyList<AccountHistoryEntryDto>>>
{
    private const string IdempotencyKeyHeader = "idempotency-key"; // mirrors EventSourcing.EventHeaders (Application must not reference Marten's project)

    private readonly IAccountRepository _accounts = accounts;

    public async ValueTask<Result<IReadOnlyList<AccountHistoryEntryDto>>> Handle(GetAccountHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _accounts.HistoryAsync(request.AccountId, cancellationToken);
        if (history.Count == 0)
        {
            return Result<IReadOnlyList<AccountHistoryEntryDto>>.NotFound($"Account {request.AccountId} was not found.");
        }

        IReadOnlyList<AccountHistoryEntryDto> entries = history
            .Select(e => new AccountHistoryEntryDto(
                e.Version,
                e.EventType,
                e.Timestamp,
                e.CorrelationId,
                e.Headers.TryGetValue(IdempotencyKeyHeader, out var key) ? key?.ToString() : null,
                e.Data))
            .ToList();

        return Result.Success(entries);
    }
}
