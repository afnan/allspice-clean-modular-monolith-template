using System.Net.Http;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;

/// <summary>
/// Composition helpers for wiring the identity module into a host application.
/// </summary>
public static class IdentityModuleExtensions
{
    private const string DatabaseResourceName = "identitydb";
    internal const string KeycloakHttpClientName = "keycloak-directory";

    /// <summary>
    /// Registers identity module services including persistence, external directory integration, and authorization helpers.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="logger">Logger used to record registration diagnostics.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    public static IHostApplicationBuilder AddIdentityModuleServices(
        this IHostApplicationBuilder builder,
        ILogger logger)
    {
        // Register the context with Wolverine's EF Core integration so this module's database hosts its
        // OWN durable outbox tables (see IdentityDbContext.OnModelCreating + the gateway's ancillary-store
        // registration), giving a true transactional outbox. The (IServiceProvider, options) overload lets
        // us keep attaching the cross-cutting EF Core save interceptors (audit + concurrency) registered via
        // AddSharedKernelInterceptors — EF Core does NOT auto-discover IInterceptor services from DI.
        var connectionString = builder.Configuration[$"ConnectionStrings:{DatabaseResourceName}"];
        builder.Services.AddDbContextWithWolverineIntegration<IdentityDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));

        // Layer Aspire's Npgsql enrichment (OpenTelemetry tracing/metrics + health check + command timeout)
        // onto the Wolverine-integrated context. Retry is DISABLED on purpose: the transactional-outbox flow
        // uses user-initiated transactions (TransactionBehavior, the dispatcher's delivered-tx), which Npgsql's
        // retrying execution strategy forbids — and retrying a block that performs an external send would
        // duplicate it. Connection-level retry would mean wrapping each transaction in an execution strategy
        // (excluding sends); deferred. See TODOS.md.
        builder.EnrichNpgsqlDbContext<IdentityDbContext>(settings =>
        {
            settings.DisableRetry = true;
            // The explicit DbContextHealthCheck<IdentityDbContext> ("identity-db") below already covers
            // connectivity (and carries the name the gateway's health filtering uses) — don't add a duplicate.
            settings.DisableHealthChecks = true;
        });
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IPermissionMapStore, PermissionMapStore>();

        // Permission map cache (singleton) + per-request permission resolver (scoped).
        // AddHttpContextAccessor uses TryAdd — safe to call even if the gateway registered it already.
        // AddMemoryCache also uses TryAdd — no double-registration risk.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IPermissionMapCache, PermissionMapCache>();
        builder.Services.AddScoped<ICurrentUserPermissions, CurrentUserPermissions>();
        builder.Services.AddScoped<IAuthorizationContext, AuthorizationContext>();
        builder.Services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();

        // New services
        builder.Services.AddScoped<IUserLookupService, UserLookupService>();
        builder.Services.AddScoped<IUserAccessService, UserAccessService>();
        builder.Services.AddScoped<IUserExternalIdResolver, UserLookupService>();

        builder.Services.AddMediator();

        builder.Services.AddValidatorsFromAssembly(AppAssemblyReference.Assembly);

        builder.Services
            .AddOptions<KeycloakOptions>()
            .Bind(builder.Configuration.GetSection("Identity:Keycloak"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<KeycloakOptions>, KeycloakOptionsValidator>();

        builder.Services
            .AddOptions<IdentitySyncOptions>()
            .Bind(builder.Configuration.GetSection(IdentitySyncOptions.ConfigurationSectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Token provider (singleton — caches tokens across requests)
        builder.Services.AddSingleton<KeycloakTokenProvider>();
        builder.Services.AddTransient<KeycloakTokenHandler>();

        // HTTP clients with token handler for auto Bearer injection
        builder.Services.AddHttpClient(KeycloakHttpClientName, ConfigureKeycloakClient)
            .AddHttpMessageHandler<KeycloakTokenHandler>()
            .ConfigurePrimaryHttpMessageHandler(CreateKeycloakHandler);

        builder.Services.AddHttpClient<KeycloakDirectoryClient>(ConfigureKeycloakClient)
            .AddHttpMessageHandler<KeycloakTokenHandler>()
            .ConfigurePrimaryHttpMessageHandler(CreateKeycloakHandler);

        builder.Services.AddScoped<IExternalDirectoryClient>(sp =>
            sp.GetRequiredService<KeycloakDirectoryClient>());

        builder.Services.AddHealthChecks()
            .AddCheck<KeycloakHealthCheck>("keycloak")
            .AddCheck<IdentitySyncHealthCheck>("identity-sync")
            .AddCheck<IdentityOrphanHealthCheck>("identity-orphans")
            .AddCheck<SharedKernel.HealthChecks.DbContextHealthCheck<IdentityDbContext>>("identity-db");

        RegisterKeycloakSync(builder);

        logger.LogInformation("Identity module services registered");

        return builder;
    }

    private static void RegisterKeycloakSync(IHostApplicationBuilder builder)
    {
        var cronExpression = builder.Configuration[$"{IdentitySyncOptions.ConfigurationSectionName}:CronExpression"]
            ?? new IdentitySyncOptions().CronExpression;

        builder.Services.AddQuartz(q =>
        {
            var jobKey = new JobKey(KeycloakUserSyncJob.JobIdentity);

            q.AddJob<KeycloakUserSyncJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{KeycloakUserSyncJob.JobIdentity}-trigger")
                .WithCronSchedule(cronExpression, builder =>
                    builder.InTimeZone(TimeZoneInfo.Utc)));
        });
    }

    private static void ConfigureKeycloakClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Cap admin-API calls so a hung Keycloak doesn't block invitation flows forever.
        // The Wolverine retry policy + Quartz refire handle the rest.
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds <= 0
            ? 30
            : options.RequestTimeoutSeconds);

        // Until the IdP is linked, AdminApiBaseAddress is null — leave the base address unset so this client
        // can still be CONSTRUCTED. The gateway's CurrentUserResolutionMiddleware resolves it on every request
        // (incl. anonymous /health); throwing here would 500 those. No call is made while unlinked (health
        // checks short-circuit on IsAdminConfigured; the sync job is idle), so an unset base is harmless.
        if (options.AdminApiBaseAddress is { } baseAddress)
        {
            client.BaseAddress = baseAddress;
        }
    }

    private static HttpMessageHandler CreateKeycloakHandler(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        if (!options.AllowUntrustedCertificates)
        {
            return new HttpClientHandler();
        }

        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    }

    /// <summary>
    /// Ensures the identity database exists and migrations are applied. Runs the
    /// shared <see cref="MigrationRunner.RunWithRetryAsync"/> with linear backoff so
    /// startup tolerates a slow-to-come-up Postgres container.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The application instance to continue fluent configuration.</returns>
    public static async Task<WebApplication> EnsureIdentityModuleDatabaseAsync(this WebApplication app)
    {
        await MigrationRunner.RunForModuleAsync<IdentityDbContext>(
            app.Services,
            app.Lifetime,
            loggerCategory: "IdentityDatabase");

        return app;
    }
}
