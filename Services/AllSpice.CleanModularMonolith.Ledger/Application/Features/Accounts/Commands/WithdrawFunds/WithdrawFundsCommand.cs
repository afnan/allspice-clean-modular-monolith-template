using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.WithdrawFunds;

public sealed record WithdrawFundsCommand(Guid AccountId, decimal Amount, string Reference) : IRequest<Result>, ITransactional;
