using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Application.DTOs;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Repositories;

public sealed class AccountRepository(IModuleEventStoreSession<ILedgerEventStore> session)
    : MartenEventSourcedRepository<Account, ILedgerEventStore>(session), IAccountRepository
{
    public async Task<AccountSummaryDto?> GetSummaryAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var query = Session.OpenQuerySession();
        var summary = await query.LoadAsync<AccountSummary>(accountId, cancellationToken);
        return summary is null
            ? null
            : new AccountSummaryDto(summary.Id, summary.OwnerUserId, summary.Currency, summary.Balance,
                summary.Status, summary.Version, summary.LastActivityUtc);
    }
}
