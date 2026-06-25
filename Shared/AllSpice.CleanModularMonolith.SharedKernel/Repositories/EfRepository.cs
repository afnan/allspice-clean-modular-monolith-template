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
public class EfRepository<TContext, TAggregate>(TContext dbContext) :
    RepositoryBase<TAggregate>(dbContext),
    IReadRepository<TAggregate>,
    IRepository<TAggregate>
    where TContext : DbContext, IModuleDbContext
    where TAggregate : class, IAggregateRoot
{
    /// <summary>
    /// Track-only: the inherited write methods (<c>AddAsync</c>/<c>UpdateAsync</c>/<c>DeleteAsync</c>)
    /// call this to persist, but here it is a no-op so they only STAGE entities in the change tracker.
    /// The unit-of-work boundary — the single real <c>SaveChanges</c> + <c>Commit</c> — is owned by
    /// <c>TransactionBehavior</c>, which calls <c>SaveChanges</c> on the module <see cref="DbContext"/>
    /// directly inside one transaction. This is what makes <c>ITransactional</c> commands atomic:
    /// nothing is written until the behavior commits, so any failure rolls back every change together.
    /// Reads are unaffected. Background/seed code that needs an immediate flush must call
    /// <c>SaveChangesAsync</c> on the <see cref="DbContext"/> itself, not through the repository.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}


