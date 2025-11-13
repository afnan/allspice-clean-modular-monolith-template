using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.SharedKernel.Common;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

/// <summary>
/// Generic Entity Framework repository that applies Ardalis.Specification to aggregate roots.
/// </summary>
/// <typeparam name="TContext">DbContext type serving the aggregate.</typeparam>
/// <typeparam name="TAggregate">Aggregate root type.</typeparam>
public class EfRepository<TContext, TAggregate> :
    RepositoryBase<TAggregate>,
    IReadRepository<TAggregate>,
    IRepository<TAggregate>
    where TContext : DbContext
    where TAggregate : AggregateRoot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EfRepository{TContext,TAggregate}"/> class.
    /// </summary>
    /// <param name="dbContext">The DbContext instance.</param>
    public EfRepository(TContext dbContext)
        : base(dbContext)
    {
    }
}


