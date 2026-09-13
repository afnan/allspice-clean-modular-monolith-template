using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

namespace AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;

/// <summary>
/// Bespoke repository for the event-sourced <see cref="Account"/> (golden rule 4). Writes go through the
/// inherited stream API; reads of current state go through the inline <c>AccountSummary</c> projection —
/// never by replaying the stream in a query.
/// </summary>
public interface IAccountRepository : IEventSourcedRepository<Account>
{
    /// <summary>Current-state read model, or <c>null</c> when the account does not exist.</summary>
    Task<AccountSummaryDto?> GetSummaryAsync(Guid accountId, CancellationToken cancellationToken = default);
}
