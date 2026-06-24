using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AllSpice.CleanModularMonolith.SharedKernel.HealthChecks;

/// <summary>
/// Health check that verifies real database connectivity for <typeparamref name="TContext"/> by
/// resolving the context from a short-lived DI scope and calling <c>CanConnectAsync</c>.
/// Works with the scoped <c>AddNpgsqlDbContext</c> registration the modules use (no
/// <see cref="IDbContextFactory{TContext}"/> required). Register via
/// <c>AddHealthChecks().AddCheck&lt;DbContextHealthCheck&lt;TContext&gt;&gt;("name")</c>.
/// </summary>
/// <typeparam name="TContext">The module DbContext type.</typeparam>
public sealed class DbContextHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Could not connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
