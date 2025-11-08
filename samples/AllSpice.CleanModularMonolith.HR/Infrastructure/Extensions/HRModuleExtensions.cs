using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AllSpice.CleanModularMonolith.HR.Application;
using AllSpice.CleanModularMonolith.HR.Infrastructure.Persistence;

namespace AllSpice.CleanModularMonolith.HR.Infrastructure.Extensions;

public static class HRModuleExtensions
{
    public static IHostApplicationBuilder AddHRModuleServices(
        this IHostApplicationBuilder builder,
        ILogger logger)
    {
        // Add PostgreSQL DbContext with Aspire
        builder.AddNpgsqlDbContext<HRDbContext>("hrdb");

        // Register Application services
        builder.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        // Register FluentValidation - use typeof to get assembly from static class
        builder.Services.AddValidatorsFromAssembly(typeof(AllSpice.CleanModularMonolith.HR.Application.AssemblyReference).Assembly);

        // Register Infrastructure services (repositories, etc.)
        // Will be added when we create them

        logger.LogInformation("HR module services registered");

        return builder;
    }

    public static async Task<WebApplication> EnsureHRModuleDatabaseAsync(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HRDbContext>();
        await context.Database.EnsureCreatedAsync();

        return app;
    }
}


