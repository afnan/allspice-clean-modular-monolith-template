using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.CloseAccount;

public sealed record CloseAccountCommand(Guid AccountId) : IRequest<Result>, ITransactional;
