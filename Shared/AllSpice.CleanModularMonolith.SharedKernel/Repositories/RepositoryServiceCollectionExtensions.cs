using AllSpice.CleanModularMonolith.SharedKernel.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

/// <summary>
/// DI helpers for registering generic <see cref="EfRepository{TContext,TAggregate}"/> instances
/// across the standard <see cref="IRepository{T}"/> and <see cref="IReadRepository{T}"/> services.
/// </summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the generic EF repository for an aggregate as scoped, and forwards both
    /// <see cref="IRepository{T}"/> and <see cref="IReadRepository{T}"/> to the same instance
    /// per scope. Use this from each module's DI bootstrap for aggregates that don't need a
    /// bespoke repository class.
    /// </summary>
    public static IServiceCollection AddEfRepository<TContext, TAggregate>(this IServiceCollection services)
        where TContext : DbContext
        where TAggregate : AggregateRoot
    {
        services.AddScoped<EfRepository<TContext, TAggregate>>();
        services.AddScoped<IRepository<TAggregate>>(sp => sp.GetRequiredService<EfRepository<TContext, TAggregate>>());
        services.AddScoped<IReadRepository<TAggregate>>(sp => sp.GetRequiredService<EfRepository<TContext, TAggregate>>());
        return services;
    }
}
