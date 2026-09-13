using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Queries.GetAccountSummary;

public sealed record GetAccountSummaryQuery(Guid AccountId) : IRequest<Result<AccountSummaryDto>>;
