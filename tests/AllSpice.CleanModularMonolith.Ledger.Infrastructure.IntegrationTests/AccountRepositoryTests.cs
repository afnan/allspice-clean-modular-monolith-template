using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
using JasperFx;
using JasperFx.Events;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests;

public sealed class AccountRepositoryTests(LedgerHostFixture fixture) : IClassFixture<LedgerHostFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 11, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public async Task Inline_projection_matches_the_stream_after_commit()
    {
        var id = await OpenAsync(deposits: [100m, 50m], withdrawals: [30m]);

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var summary = await repo.GetSummaryAsync(id);

        Assert.NotNull(summary);
        Assert.Equal(120m, summary.Balance);
        Assert.Equal("AUD", summary.Currency);
        Assert.Equal("Open", summary.Status);
        Assert.Equal(4, summary.Version); // opened + 2 deposits + 1 withdrawal
        Assert.Equal(Owner, summary.OwnerUserId);
    }

    [Fact]
    public async Task Summary_is_null_for_unknown_account()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        Assert.Null(await repo.GetSummaryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Closing_archives_the_stream_and_the_summary_reads_Closed()
    {
        var id = await OpenAsync();

        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var account = (await repo.LoadAsync(id))!;
            account.Close(Now);
            await repo.SaveAsync(account);
            await CommitAsync(scope);
        }

        await using var verify = fixture.Host.Services.CreateAsyncScope();
        var summary = await verify.ServiceProvider.GetRequiredService<IAccountRepository>().GetSummaryAsync(id);
        Assert.Equal("Closed", summary!.Status);

        await using var query = fixture.Host.Services.GetRequiredService<ILedgerEventStore>().QuerySession();
        Assert.True((await query.Events.FetchStreamStateAsync(id))!.IsArchived);
    }

    [Fact]
    public async Task Legacy_funds_deposited_rows_upcast_on_load_and_in_history()
    {
        var id = await OpenAsync();

        // Write a row under the OLD stored name the way a pre-versioning deployment would have — through a
        // SEPARATE, throwaway store (same connection + schema) configured with ONLY the mapping a legacy
        // writer would have had, NOT through fixture.Host's store. The host's store must stay on the
        // unmodified, production LedgerEventStoreConfiguration.Configure — it only knows FundsDepositedV1 as
        // an Upcast source, never as something it would itself append — so the read below exercises exactly
        // the production upcast path, not a test-only writer mapping smuggled into the read side too.
        // Note: this writer store stamps mt_dotnet_type as ...Legacy.FundsDepositedV1, whereas a real
        // pre-versioning row would carry ...Events.FundsDeposited (the type as it was named before the
        // rename to Legacy.FundsDepositedV1); irrelevant here because production resolves upcasting by the
        // stored "type" NAME ("funds_deposited"), never by mt_dotnet_type.
        await using (var writerStore = DocumentStore.For(opts =>
        {
            opts.Connection(fixture.ConnectionString);
            opts.DatabaseSchemaName = LedgerEventStoreConfiguration.SchemaName;
            opts.AutoCreateSchemaObjects = AutoCreate.None;
            opts.UseSystemTextJsonForSerialization();
            opts.Events.AppendMode = EventAppendMode.Rich;
            opts.Events.MapEventType<FundsDepositedV1>("funds_deposited");
        }))
        await using (var session = writerStore.LightweightSession())
        {
            session.Events.Append(id, new FundsDepositedV1(id, 42m, "AUD", Now));
            await session.SaveChangesAsync();
        }

        // From here on, everything reads through the PRODUCTION-configured host store/repository.
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account = (await repo.LoadAsync(id))!;
        Assert.Equal(42m, account.Balance.Amount);

        var history = await repo.HistoryAsync(id);
        var upcast = Assert.IsType<FundsDeposited>(history[^1].Data);
        Assert.Equal(FundsDepositedUpcasts.LegacyReference, upcast.Reference);
        Assert.Equal("funds_deposited", history[^1].EventType);

        // The inline AccountSummaryProjection has no Apply(IEvent<FundsDepositedV1>, ...) overload — it only
        // knows FundsDeposited (the current shape) — so a raw legacy-shape append never touches the summary
        // document. GetSummaryAsync for this stream is intentionally stale/unaffected by this test.

        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.CurrentTransaction!.RollbackAsync();
    }

    [Fact]
    public async Task Current_deposits_are_stored_under_the_v2_name()
    {
        var id = await OpenAsync(deposits: [1m]);

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var history = await scope.ServiceProvider.GetRequiredService<IAccountRepository>().HistoryAsync(id);

        Assert.Equal("funds_deposited_v2", history[^1].EventType);
    }

    [Fact]
    public async Task Schema_apply_is_idempotent()
    {
        await fixture.Host.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>();
        await fixture.Host.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>();
    }

    private async Task<Guid> OpenAsync(decimal[]? deposits = null, decimal[]? withdrawals = null)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Open(Guid.NewGuid(), Owner, Currency.Aud, Now);
        foreach (var d in deposits ?? []) account.Deposit(Money.Of(d, Currency.Aud), "seed", Now);
        foreach (var w in withdrawals ?? []) account.Withdraw(Money.Of(w, Currency.Aud), "seed", Now);
        await repo.AddAsync(account);
        await CommitAsync(scope);
        return account.Id;
    }

    private static async Task CommitAsync(AsyncServiceScope scope)
    {
        await scope.ServiceProvider.GetRequiredService<AllSpice.CleanModularMonolith.SharedKernel.Persistence.ITransactionParticipant>().FlushAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.CurrentTransaction!.CommitAsync();
    }
}
