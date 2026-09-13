using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountHistory;

public sealed record GetAccountHistoryQuery(Guid AccountId) : IRequest<Result<IReadOnlyList<AccountHistoryEntryDto>>>;
