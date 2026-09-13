using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;
using JasperFx.Events.Projections;
using Marten;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// Everything Marten needs to know about the Ledger stream: projections, stored event-type names and
/// upcasters. Kept separate from DI so the integration tests can build a store with exactly the production
/// configuration.
/// </summary>
public static class LedgerEventStoreConfiguration
{
    public const string SchemaName = "ledger";

    public static void Configure(StoreOptions opts)
    {
        // Explicit write-model registration: without this, an ancillary store with only an inline
        // projection registered can still leave ApplyEventStoreSchemaAsync's "is anything active" guard
        // unsatisfied in edge cases, and it documents the write side (FetchForWriting<Account>) alongside
        // the read side below.
        opts.Projections.LiveStreamAggregation<Account>();

        // Current-state read model, updated in the same transaction as the append.
        opts.Projections.Add(new AccountSummaryProjection(), ProjectionLifecycle.Inline);

        // Event versioning (the template's worked example — ADR-0009):
        //  * the CURRENT shape keeps the canonical CLR name but gets a NEW stored name so it never collides
        //    with rows written under the old shape;
        //  * the OLD shape is kept as a *V1 CLR type and upcast on read. Marten's default naming would map
        //    both FundsDeposited and FundsDepositedV1 to snake_case names that don't match history, so both
        //    are pinned explicitly.
        opts.Events.MapEventType<FundsDeposited>("funds_deposited_v2");
        opts.Events.Upcast<FundsDepositedV1, FundsDeposited>("funds_deposited", FundsDepositedUpcasts.FromV1);
    }
}
