using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.DepositFunds;

public sealed record DepositFundsCommand(Guid AccountId, decimal Amount, string Reference) : IRequest<Result>, ITransactional;
