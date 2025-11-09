using System;
using System.Net.Http.Headers;
using Ardalis.GuardClauses;
using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AppAssemblyReference = AllSpice.CleanModularMonolith.Identity.Application.AssemblyReference;
using DomainModuleDefinition = AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition.ModuleDefinition;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;

public static class IdentityModuleExtensions
{
    private const string DatabaseResourceName = "identitydb";

    public static IHostApplicationBuilder AddIdentityModuleServices(
        this IHostApplicationBuilder builder,
        ILogger logger)
    {
        builder.AddNpgsqlDbContext<IdentityDbContext>(DatabaseResourceName);

        builder.Services.AddScoped<IModuleDefinitionRepository, ModuleDefinitionRepository>();
        builder.Services.AddScoped<IModuleRoleAssignmentRepository, ModuleRoleAssignmentRepository>();
        builder.Services.AddScoped<IExternalDirectoryClient, AuthentikDirectoryClient>();

        builder.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        builder.Services.AddValidatorsFromAssembly(AppAssemblyReference.Assembly);
        builder.Services.AddModuleRoleAuthorization();

        builder.Services.Configure<AuthentikOptions>(builder.Configuration.GetSection("Identity:Authentik"));

        builder.Services.AddHttpClient<AuthentikDirectoryClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AuthentikOptions>>().Value;
            Guard.Against.NullOrWhiteSpace(options.BaseUrl, nameof(options.BaseUrl));
            Guard.Against.NullOrWhiteSpace(options.ApiToken, nameof(options.ApiToken));

            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AuthentikOptions>>().Value;
            if (!options.AllowUntrustedCertificates)
            {
                return new HttpClientHandler();
            }

            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        });

        logger.LogInformation("Identity module services registered");

        return builder;
    }

    public static async Task<WebApplication> EnsureIdentityModuleDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.EnsureCreatedAsync();

        if (!await context.ModuleDefinitions.AnyAsync())
        {
            var hrModule = DomainModuleDefinition.Create("HR", "Human Resources", "HR service module");
            hrModule.AddRole("Admin", "HR Administrator", "Full access to HR module");
            hrModule.AddRole("Employee", "Employee", "Standard employee access");

            var financeModule = DomainModuleDefinition.Create("Finance", "Finance", "Finance module");
            financeModule.AddRole("Admin", "Finance Administrator", "Full finance access");
            financeModule.AddRole("Analyst", "Finance Analyst", "Read-only finance access");

            var eventsModule = DomainModuleDefinition.Create("Events", "Events", "Events and communications");
            eventsModule.AddRole("Admin", "Events Administrator", "Full events access");
            eventsModule.AddRole("Viewer", "Events Viewer", "View-only access");

            await context.ModuleDefinitions.AddRangeAsync(hrModule, financeModule, eventsModule);
            await context.SaveChangesAsync();
        }

        return app;
    }
}


