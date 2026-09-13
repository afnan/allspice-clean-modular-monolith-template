using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// The Ledger module's EF Core context. It has NO entities of its own — the only aggregate is event-sourced —
/// but it still exists because it (1) owns the module transaction that the Marten session enlists in and
/// (2) hosts the module's co-located Wolverine outbox tables. A real module would add its ordinary EF
/// aggregates here alongside the event-sourced ones.
/// </summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options), IModuleDbContext
{
    DbContext IModuleDbContext.Instance => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Co-locate the Wolverine durable outbox tables in this module's own database so integration
        // events commit in the SAME transaction as the events appended to the Marten store.
        if (Database.IsNpgsql())
        {
            modelBuilder.MapWolverineEnvelopeStorage("wolverine");
        }
    }
}
