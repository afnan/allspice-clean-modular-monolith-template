using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Interceptors;

/// <summary>
/// Verifies that the save interceptors registered by <see cref="SharedKernelInterceptorExtensions.AddSharedKernelInterceptors"/>
/// are actually discovered and applied by EF Core on a POOLED DbContext — the same registration shape Aspire's
/// <c>AddNpgsqlDbContext</c> uses (<c>AddDbContextPool</c> + the application service provider).
/// </summary>
public sealed class SaveInterceptorDiscoveryTests
{
    private sealed class AuditProbe : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ProbeDbContext : DbContext
    {
        public ProbeDbContext(DbContextOptions<ProbeDbContext> options) : base(options)
        {
        }

        public DbSet<AuditProbe> Probes => Set<AuditProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditProbe>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Ignore(p => p.DomainEvents);
            });
        }
    }

    private sealed class StubCurrentUserProvider : ICurrentUserProvider
    {
        public string? UserId { get; init; }
    }

    [Fact]
    public async Task AuditableEntityInterceptor_is_discovered_and_stamps_user_on_a_pooled_context()
    {
        const string expectedUserId = "user-123";

        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserProvider>(new StubCurrentUserProvider { UserId = expectedUserId });
        services.AddSharedKernelInterceptors();
        services.AddDbContextPool<ProbeDbContext>((sp, options) =>
            options.UseSqlite(connection).AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            await context.Database.EnsureCreatedAsync();
            context.Probes.Add(new AuditProbe { Name = "probe" });
            await context.SaveChangesAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            var probe = await context.Probes.SingleAsync();

            // CreatedBy is only set by the DI-registered AuditableEntityInterceptor firing on save.
            Assert.Equal(expectedUserId, probe.CreatedBy);
        }

        await connection.DisposeAsync();
    }
}
