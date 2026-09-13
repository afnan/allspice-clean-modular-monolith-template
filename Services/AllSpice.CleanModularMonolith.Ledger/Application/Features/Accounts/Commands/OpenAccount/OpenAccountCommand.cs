using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Features.Accounts.Commands.OpenAccount;

/// <param name="OwnerUserId">Local user UUID (User.Id) — not the Keycloak external id.</param>
/// <param name="Currency">ISO 4217 alpha code (AUD, USD, EUR).</param>
public sealed record OpenAccountCommand(Guid OwnerUserId, string Currency) : IRequest<Result<Guid>>, ITransactional;
