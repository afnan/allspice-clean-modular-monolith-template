using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Interceptors;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Interceptors;

/// <summary>
/// Verifies that <see cref="SoftDeleteInterceptor"/> turns a hard delete of an <see cref="ISoftDelete"/> entity
/// into a soft delete: the row survives (no SQL DELETE), is flagged <c>IsDeleted</c> + stamped with the current
/// user, and is hidden by the global query filter.
/// </summary>
public sealed class SoftDeleteInterceptorTests
{
    private sealed class SoftDeleteProbe : SoftDeletableEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
    {
        public DbSet<SoftDeleteProbe> Probes => Set<SoftDeleteProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftDeleteProbe>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Ignore(p => p.DomainEvents);
            });

            modelBuilder.ApplySoftDeleteFilters();
        }
    }

    private sealed class StubCurrentUserProvider : ICurrentUserProvider
    {
        public string? UserId { get; init; }
    }

    [Fact]
    public async Task Removing_a_soft_deletable_entity_soft_deletes_it_instead_of_hard_deleting()
    {
        const string expectedUserId = "user-123";

        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserProvider>(new StubCurrentUserProvider { UserId = expectedUserId });
        services.AddSharedKernelInterceptors();
        services.AddDbContextPool<ProbeDbContext>((sp, options) =>
            options.UseSqlite(connection).AddInterceptors(sp.GetServices<IInterceptor>()));

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            await context.Database.EnsureCreatedAsync();
            context.Probes.Add(new SoftDeleteProbe { Name = "probe" });
            await context.SaveChangesAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            var probe = await context.Probes.SingleAsync();
            context.Probes.Remove(probe);
            await context.SaveChangesAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

            // Hidden by the global query filter...
            Assert.False(await context.Probes.AnyAsync());

            // ...but still present (not hard-deleted), flagged deleted, and stamped with the current user.
            var probe = await context.Probes.IgnoreQueryFilters().SingleAsync();
            Assert.True(probe.IsDeleted);
            Assert.NotNull(probe.DeletedOnUtc);
            Assert.Equal(expectedUserId, probe.DeletedBy);
        }

        await connection.DisposeAsync();
    }
}
