using System.Linq.Expressions;
using AllSpice.CleanModularMonolith.SharedKernel.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

public static class SoftDeleteQueryFilterConvention
{
    /// <summary>
    /// Applies a global query filter to all entity types implementing <see cref="ISoftDelete"/>
    /// so that soft-deleted rows are excluded from queries by default.
    /// Use <c>.IgnoreQueryFilters()</c> to include deleted rows when needed.
    /// </summary>
    public static ModelBuilder ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }

        return modelBuilder;
    }
}
