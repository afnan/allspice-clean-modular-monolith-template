using Marten;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// Marker for the Ledger module's Marten store (registered with <c>AddMartenStore&lt;ILedgerEventStore&gt;</c>).
/// Lives in <c>ledgerdb</c>, schema <c>ledger</c>, and enlists in <see cref="LedgerDbContext"/>'s transaction.
/// </summary>
public interface ILedgerEventStore : IDocumentStore
{
}
