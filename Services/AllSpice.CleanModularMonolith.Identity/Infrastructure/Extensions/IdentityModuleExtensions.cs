using System.Net.Http;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Quartz;
namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;

/// <summary>
/// Composition helpers for wiring the identity module into a host application.
/// </summary>
public static class IdentityModuleExtensions
{
    private const string DatabaseResourceName = "identitydb";
    internal const string AuthentikHttpClientName = "authentik-directory";

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

        builder.Services.AddScoped<IModuleDefinitionRepository, ModuleDefinitionRepository>();
        builder.Services.AddScoped<IModuleRoleAssignmentRepository, ModuleRoleAssignmentRepository>();
        builder.Services.AddScoped<IExternalDirectoryClient, AuthentikDirectoryClient>();
        builder.Services.AddScoped<IRepository<ModuleRoleTemplate>>(sp =>
        {
            var context = sp.GetRequiredService<IdentityDbContext>();
            return new EfRepository<IdentityDbContext, ModuleRoleTemplate>(context);
        });
        builder.Services.AddScoped<IReadRepository<ModuleRoleTemplate>>(sp =>
            (IReadRepository<ModuleRoleTemplate>)sp.GetRequiredService<IRepository<ModuleRoleTemplate>>());

        builder.Services.AddMediator();

        builder.Services.AddValidatorsFromAssembly(AppAssemblyReference.Assembly);
        builder.Services.AddModuleRoleAuthorization();

        builder.Services.Configure<AuthentikOptions>(builder.Configuration.GetSection("Identity:Authentik"));
        builder.Services.Configure<IdentitySyncOptions>(builder.Configuration.GetSection(IdentitySyncOptions.ConfigurationSectionName));

        builder.Services.AddHttpClient(AuthentikHttpClientName, ConfigureAuthentikClient)
            .ConfigurePrimaryHttpMessageHandler(CreateAuthentikHandler);

        builder.Services.AddHttpClient<AuthentikDirectoryClient>(ConfigureAuthentikClient)
            .ConfigurePrimaryHttpMessageHandler(CreateAuthentikHandler);

        builder.Services.AddHealthChecks()
            .AddCheck<AuthentikHealthCheck>("authentik")
            .AddCheck<IdentitySyncHealthCheck>("identity-sync")
            .AddCheck<IdentityOrphanHealthCheck>("identity-orphans");

        RegisterAuthentikSync(builder);

        logger.LogInformation("Identity module services registered");

        return builder;
    }

    private static void RegisterAuthentikSync(IHostApplicationBuilder builder)
    {
        var cronExpression = builder.Configuration[$"{IdentitySyncOptions.ConfigurationSectionName}:CronExpression"]
            ?? new IdentitySyncOptions().CronExpression;

        builder.Services.AddQuartz(q =>
        {
            var jobKey = new JobKey(AuthentikUserSyncJob.JobIdentity);

            q.AddJob<AuthentikUserSyncJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{AuthentikUserSyncJob.JobIdentity}-trigger")
                .WithCronSchedule(cronExpression, builder =>
                    builder.InTimeZone(TimeZoneInfo.Utc)));
        });
    }

    private static void ConfigureAuthentikClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AuthentikOptions>>().Value;
        Guard.Against.NullOrWhiteSpace(options.BaseUrl, nameof(options.BaseUrl));
        Guard.Against.NullOrWhiteSpace(options.ApiToken, nameof(options.ApiToken));

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static HttpMessageHandler CreateAuthentikHandler(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AuthentikOptions>>().Value;
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
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.EnsureCreatedAsync(app.Lifetime.ApplicationStopping);

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


