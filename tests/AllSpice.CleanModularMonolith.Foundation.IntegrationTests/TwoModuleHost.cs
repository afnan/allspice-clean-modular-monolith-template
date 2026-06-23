using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Interceptors;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// Builds a DI scope that registers BOTH module DbContexts (Notifications first, Identity second —
/// mirroring the gateway's registration order). This is the condition that hides the unit-of-work
/// bug: the original TransactionBehavior opened its transaction on the first IModuleDbContext, which
/// is Notifications, even for an Identity command. The module unit/integration test suites register
/// one module at a time, so they never exercise this.
/// </summary>
public sealed class TwoModuleHost
{
    public IServiceProvider Services { get; }

    private TwoModuleHost(IServiceProvider services) => Services = services;

    public static async Task<TwoModuleHost> CreateAsync(PostgresFixture pg)
    {
        var notificationsCs = await pg.CreateDatabaseAsync("notif_" + Guid.NewGuid().ToString("N"));
        var identityCs = await pg.CreateDatabaseAsync("ident_" + Guid.NewGuid().ToString("N"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserProvider, TestCurrentUserProvider>();
        services.AddSharedKernelInterceptors();
        services.AddSingleton<IDomainEventDispatcher, NoOpDomainEventDispatcher>();

        // Notifications FIRST, Identity SECOND — mirrors GatewayModuleRegistrationExtensions.
        services.AddDbContext<NotificationsDbContext>((sp, o) =>
            o.UseNpgsql(notificationsCs).AddInterceptors(sp.GetServices<IInterceptor>()));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

        services.AddDbContext<IdentityDbContext>((sp, o) =>
            o.UseNpgsql(identityCs).AddInterceptors(sp.GetServices<IInterceptor>()));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
        }

        return new TwoModuleHost(provider);
    }
}

internal sealed class TestCurrentUserProvider : ICurrentUserProvider
{
    public string? UserId => "00000000-0000-0000-0000-000000000001";
}

internal sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
