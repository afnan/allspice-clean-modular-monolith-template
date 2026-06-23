using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

/// <summary>
/// OPT-IN alternative dispatch strategy: dispatches domain events <b>after</b> a successful
/// <see cref="DbContext.SaveChangesAsync"/>, resolving <see cref="IDomainEventDispatcher"/> from a fresh
/// DI scope (so it works in background/Quartz flows and with <c>AddDbContextFactory</c>, which have no
/// request scope).
///
/// <para><b>Do not enable this together with the default pre-commit dispatch in
/// <c>TransactionBehavior</c></b> — doing so dispatches every event twice. Use this only if you switch a
/// context off the <c>ITransactional</c>/<c>TransactionBehavior</c> path. Because events fire after commit,
/// handlers must be idempotent; dispatch uses <see cref="CancellationToken.None"/> so a client disconnect
/// can't leave the system half-reconciled.</para>
/// </summary>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DomainEventDispatchInterceptor(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchEventsAsync(eventData.Context);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchEventsAsync(DbContext context)
    {
        var entitiesWithEvents = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        if (entitiesWithEvents.Count == 0)
        {
            return;
        }

        var domainEvents = entitiesWithEvents.SelectMany(e => e.TakeDomainEvents()).ToList();

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);
    }
}
