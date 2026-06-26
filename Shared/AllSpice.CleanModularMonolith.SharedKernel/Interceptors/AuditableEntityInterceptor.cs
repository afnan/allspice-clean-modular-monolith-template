using AllSpice.CleanModularMonolith.SharedKernel.Auditing;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable"/> entities on save via EF Core's change tracker (<c>PropertyEntry</c>):
/// <c>CreatedOnUtc</c>/<c>CreatedBy</c> for added entities, <c>LastModifiedOnUtc</c>/<c>LastModifiedBy</c> for
/// modified ones, using the current user from <see cref="ICurrentUserProvider"/>. The audit columns are
/// read-only on the domain — both the timestamp and the user are written here, never by domain code.
/// Registered as a singleton and discovered by EF Core from the application service provider, so it works
/// with pooled DbContexts.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUserProvider currentUserProvider) : SaveChangesInterceptor
{
    private readonly ICurrentUserProvider _currentUserProvider = currentUserProvider;

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
        var now = DateTimeOffset.UtcNow;

        // Audit columns are read-only on the domain (IAuditable exposes no mutators), so stamp them through
        // EF's change tracker rather than a domain method — audit stays a pure persistence concern.
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedOnUtc)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = userId;
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.LastModifiedOnUtc)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.LastModifiedBy)).CurrentValue = userId;
                    break;
            }
        }
    }
}
