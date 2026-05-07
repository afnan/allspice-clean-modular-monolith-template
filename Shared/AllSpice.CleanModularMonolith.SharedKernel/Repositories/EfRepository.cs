using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

/// <summary>
/// Generic Entity Framework repository that applies Ardalis.Specification to aggregate roots.
/// The <typeparamref name="TContext"/> constraint enforces module isolation: only DbContext
/// types that opt into <see cref="IModuleDbContext"/> can back a repository, which is the
/// same surface <c>TransactionBehavior</c> uses to enroll a transaction.
/// </summary>
/// <typeparam name="TContext">DbContext type serving the aggregate. Must implement <see cref="IModuleDbContext"/>.</typeparam>
/// <typeparam name="TAggregate">Aggregate root type.</typeparam>
public class EfRepository<TContext, TAggregate> :
    RepositoryBase<TAggregate>,
    IReadRepository<TAggregate>,
    IRepository<TAggregate>
    where TContext : DbContext, IModuleDbContext
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


