using AllSpice.CleanModularMonolith.SharedKernel.Auditing;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable"/> entities with the current user (from <see cref="ICurrentUserProvider"/>)
/// on save: <c>SetCreated</c> for added entities, <c>SetModified</c> for modified ones. Registered as a
/// singleton and discovered by EF Core from the application service provider, so it works with pooled
/// DbContexts. Timestamps are set by the entity itself; this fills in the user identifier.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public AuditableEntityInterceptor(ICurrentUserProvider currentUserProvider)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var userId = _currentUserProvider.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreated(userId);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetModified(userId);
                    break;
            }
        }
    }
}
