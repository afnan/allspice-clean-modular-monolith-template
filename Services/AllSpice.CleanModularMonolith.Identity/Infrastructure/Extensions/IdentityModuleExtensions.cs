using System.Net.Http;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Quartz;
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
        builder.AddNpgsqlDbContext<IdentityDbContext>(DatabaseResourceName);

        // Existing repositories
        builder.Services.AddScoped<IModuleDefinitionRepository, ModuleDefinitionRepository>();
        builder.Services.AddScoped<IModuleRoleAssignmentRepository, ModuleRoleAssignmentRepository>();
        builder.Services.AddScoped<IRepository<ModuleRoleTemplate>>(sp =>
        {
            var context = sp.GetRequiredService<IdentityDbContext>();
            return new EfRepository<IdentityDbContext, ModuleRoleTemplate>(context);
        });
        builder.Services.AddScoped<IReadRepository<ModuleRoleTemplate>>(sp =>
            (IReadRepository<ModuleRoleTemplate>)sp.GetRequiredService<IRepository<ModuleRoleTemplate>>());

        // New repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();

        // New services
        builder.Services.AddScoped<IUserLookupService, UserLookupService>();
        builder.Services.AddScoped<IUserAccessService, UserAccessService>();
        builder.Services.AddScoped<IUserExternalIdResolver, UserLookupService>();

        builder.Services.AddMediator();

        builder.Services.AddValidatorsFromAssembly(AppAssemblyReference.Assembly);
        builder.Services.AddModuleRoleAuthorization();

        builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Identity:Keycloak"));
        builder.Services.Configure<IdentitySyncOptions>(builder.Configuration.GetSection(IdentitySyncOptions.ConfigurationSectionName));

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
            .AddCheck<IdentityOrphanHealthCheck>("identity-orphans");

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
        Guard.Against.NullOrWhiteSpace(options.Realm, nameof(options.Realm));

        // Use service discovery if ServiceName is provided, otherwise use BaseUrl
        var baseUrl = !string.IsNullOrWhiteSpace(options.ServiceName)
            ? $"http://{options.ServiceName}/admin/realms/{options.Realm}"
            : (string.IsNullOrWhiteSpace(options.BaseUrl)
                ? throw new InvalidOperationException("Either ServiceName or BaseUrl must be provided for Keycloak configuration")
                : $"{options.BaseUrl.TrimEnd('/')}/admin/realms/{options.Realm}");

        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
    /// Ensures the identity database exists and seeds default module definitions when empty.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The application instance to continue fluent configuration.</returns>
    public static async Task<WebApplication> EnsureIdentityModuleDatabaseAsync(this WebApplication app)
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        var logger = loggerFactory.CreateLogger("IdentityDatabase");

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await context.Database.EnsureCreatedAsync(app.Lifetime.ApplicationStopping);

                if (!await context.ModuleDefinitions.AnyAsync())
                {
                    var identityModule = DomainModuleDefinition.Create("Identity", "Identity", "Identity and access management");
                    identityModule.AddRole("Admin", "Identity Administrator", "Full access to identity management");
                    identityModule.AddRole("Viewer", "Identity Viewer", "Read-only identity access");

                    var notificationsModule = DomainModuleDefinition.Create("Notifications", "Notifications", "Notification delivery and management");
                    notificationsModule.AddRole("Admin", "Notifications Administrator", "Full notifications access");
                    notificationsModule.AddRole("Viewer", "Notifications Viewer", "View-only notifications access");

                    await context.ModuleDefinitions.AddRangeAsync(identityModule, notificationsModule);
                    await context.SaveChangesAsync();
                }

                break;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Identity database setup attempt {Attempt}/{Max} failed. Retrying...", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), app.Lifetime.ApplicationStopping);
            }
        }

        return app;
    }
}
