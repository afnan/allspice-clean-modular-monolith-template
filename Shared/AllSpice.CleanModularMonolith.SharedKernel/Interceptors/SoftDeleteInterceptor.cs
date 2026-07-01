using AllSpice.CleanModularMonolith.SharedKernel.Auditing;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

/// <summary>
/// Translates hard deletes of <see cref="ISoftDelete"/> entities into soft deletes: a <c>Deleted</c> entry is
/// flipped to <c>Modified</c> and stamped via <see cref="ISoftDelete.MarkDeleted"/> with the current user, so a
/// <c>Remove()</c> / repository delete never issues a SQL <c>DELETE</c> for a soft-deletable row. Pairs with the
/// global query filter (<see cref="Persistence.SoftDeleteQueryFilterConvention"/>) that hides <c>IsDeleted</c>
/// rows by default. Registered as a singleton and discovered by EF Core from the application service provider,
/// so it works with pooled DbContexts. Runs before the audit interceptor so the now-modified row is also stamped.
/// </summary>
public sealed class SoftDeleteInterceptor(
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    private readonly ICurrentUserProvider _currentUserProvider = currentUserProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SoftDeleteEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SoftDeleteEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SoftDeleteEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var userId = _currentUserProvider.UserId;
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.MarkDeleted(userId, now);
        }
    }
}
