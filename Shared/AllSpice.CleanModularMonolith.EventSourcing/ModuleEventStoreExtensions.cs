using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Wires one module's Marten event store as an <b>ancillary</b> store living in that module's own database,
/// enlisted in the module DbContext transaction via <see cref="MartenTransactionParticipant{TStore,TContext}"/>.
/// A module with no event-sourced aggregates never calls this and never loads Marten.
/// </summary>
public static class ModuleEventStoreExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TStore"/> with the template's fixed settings and the transaction
    /// participant. <paramref name="configure"/> adds the module's projections, event-type mappings and
    /// upcasters.
    /// </summary>
    /// <param name="connectionString">The module database — the SAME database as <typeparamref name="TContext"/>.</param>
    /// <param name="schemaName">Postgres schema for the event tables (the module name, e.g. <c>ledger</c>); the
    /// outbox stays in <c>wolverine</c> and EF tables in <c>public</c>.</param>
    /// <param name="configure">
    /// <b>Required</b> to register at least one event type or projection — typically
    /// <c>opts.Projections.LiveStreamAggregation&lt;TAggregate&gt;()</c> per event-sourced aggregate in this
    /// module. Marten only treats its Events schema feature as active when something is registered; leaving
    /// this empty makes <see cref="ApplyEventStoreSchemaAsync{TStore}"/> throw at startup rather than silently
    /// creating no <c>mt_events</c>/<c>mt_streams</c> tables (which would otherwise surface much later as a
    /// runtime "relation does not exist" error).
    /// </param>
    public static IHostApplicationBuilder AddModuleEventStore<TStore, TContext>(
        this IHostApplicationBuilder builder,
        string connectionString,
        string schemaName,
        Action<StoreOptions>? configure = null)
        where TStore : class, IDocumentStore
        where TContext : DbContext, IModuleDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        builder.Services.AddMartenStore<TStore>(opts =>
        {
            opts.Connection(connectionString);
            opts.DatabaseSchemaName = schemaName;

            // Schema is applied explicitly at startup (ApplyEventStoreSchemaAsync), like EF migrations — never
            // lazily on first use, and never dropping anything.
            opts.AutoCreateSchemaObjects = AutoCreate.None;

            opts.UseSystemTextJsonForSerialization();

            // Marten 9 defaults to QuickWithServerTimestamps, which withholds stream versions from inline
            // projections and forbids expected-version appends. Rich mode gives deterministic versions, which
            // the projections (Version column) and optimistic concurrency rely on.
            opts.Events.AppendMode = EventAppendMode.Rich;
            opts.Events.StreamIdentity = StreamIdentity.AsGuid;

            // Correlation id + headers (Idempotency-Key) on every event — see MartenTransactionParticipant.
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;

            configure?.Invoke(opts);
        });

        // A host that has no request context (tests, jobs) gets the null provider; the gateway registers its
        // HttpContext-backed provider BEFORE modules so TryAdd leaves it in place.
        builder.Services.TryAddScoped<IEventMetadataProvider, NullEventMetadataProvider>();

        builder.Services.AddScoped<MartenTransactionParticipant<TStore, TContext>>();
        builder.Services.AddScoped<AllSpice.CleanModularMonolith.SharedKernel.Persistence.ITransactionParticipant>(sp =>
            sp.GetRequiredService<MartenTransactionParticipant<TStore, TContext>>());
        builder.Services.AddScoped<IModuleEventStoreSession<TStore>>(sp =>
            sp.GetRequiredService<MartenTransactionParticipant<TStore, TContext>>());

        return builder;
    }

    /// <summary>
    /// Creates or updates the store's tables/functions in its schema. Call from
    /// <c>Ensure{Module}ModuleDatabaseAsync</c> after the EF migration; Marten serialises concurrent callers
    /// with its own advisory lock, so multiple replicas booting together are safe.
    /// <para>
    /// Fails fast if <typeparamref name="TStore"/> has no event type or projection registered: Marten only
    /// treats its Events schema feature as active when at least one is, so with nothing registered this
    /// method would otherwise silently create no <c>mt_events</c>/<c>mt_streams</c> tables at all — even
    /// though <c>FetchForWriting&lt;T&gt;</c> still resolves an aggregate fine at runtime via live
    /// aggregation — surfacing much later as a runtime "relation does not exist" error instead of a clear
    /// startup failure.
    /// </para>
    /// </summary>
    public static async Task ApplyEventStoreSchemaAsync<TStore>(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TStore : IDocumentStore
    {
        var store = services.GetRequiredService<TStore>();

        // AllKnownEventTypes() always contains Marten's built-in `archived` marker event (JasperFx.Events.
        // Archived), so a count of exactly 1 with nothing else means no real event type was registered via
        // opts.Events.AddEventType(...) (directly, or indirectly through a projection's own event usage).
        // Projections() is the other route to activating the Events feature: non-empty once any projection
        // (e.g. LiveStreamAggregation<TAggregate>()) is registered, even if it adds no explicit event type.
        if (store.Options.Events.AllKnownEventTypes().Count <= 1 && store.Options.Events.Projections().Count == 0)
        {
            throw new InvalidOperationException(
                $"{typeof(TStore).Name} has no event types or projections registered, so its event tables would " +
                "not be created. Register at least opts.Projections.LiveStreamAggregation<TAggregate>() for each " +
                "event-sourced aggregate in the AddModuleEventStore configure callback.");
        }

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(ct: cancellationToken).ConfigureAwait(false);
    }
}
