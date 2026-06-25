using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Resend;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Extensions;

/// <summary>
/// Provides composition helpers for wiring the notifications module into the hosting application.
/// </summary>
public static class NotificationsModuleExtensions
{
    private const string DatabaseResourceName = "notificationsdb";

    /// <summary>
    /// Registers notifications module services including persistence, messaging, and background workers.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="logger">Logger used to emit registration diagnostics.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    public static IHostApplicationBuilder AddNotificationsModuleServices(
        this IHostApplicationBuilder builder,
        ILogger logger)
    {
        // Register the context with Wolverine's EF Core integration so this module's database hosts its
        // OWN durable outbox tables (see NotificationsDbContext.OnModelCreating + the gateway's ancillary-
        // store registration), giving a true transactional outbox. The (IServiceProvider, options) overload
        // lets us keep attaching the cross-cutting EF Core save interceptors (audit + concurrency) registered
        // via AddSharedKernelInterceptors — EF Core does NOT auto-discover IInterceptor services from DI.
        var connectionString = builder.Configuration[$"ConnectionStrings:{DatabaseResourceName}"];
        builder.Services.AddDbContextWithWolverineIntegration<NotificationsDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));

        // Layer Aspire's Npgsql enrichment (OpenTelemetry tracing/metrics + health check + command timeout)
        // onto the Wolverine-integrated context. Retry is DISABLED on purpose: the transactional-outbox flow
        // uses user-initiated transactions (TransactionBehavior, the dispatcher's delivered-tx), which Npgsql's
        // retrying execution strategy forbids — and retrying a block that performs an external send would
        // duplicate it. Connection-level retry would mean wrapping each transaction in an execution strategy
        // (excluding sends); deferred. See TODOS.md.
        builder.EnrichNpgsqlDbContext<NotificationsDbContext>(settings => settings.DisableRetry = true);
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        builder.Services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();

        builder.Services.AddMediator();

        builder.Services.AddValidatorsFromAssembly(typeof(AppAssemblyReference).Assembly);

        // Email provider options
        builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Notifications:Resend"));
        builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("Notifications:SendGrid"));
        builder.Services.Configure<MailKitSmtpOptions>(builder.Configuration.GetSection("Notifications:Smtp"));

        // Resend client (IResend)
        builder.Services.AddOptions<ResendClientOptions>()
            .Configure<Microsoft.Extensions.Options.IOptions<ResendOptions>>((clientOpts, resendOpts) =>
            {
                clientOpts.ApiToken = resendOpts.Value.ApiKey;
            });
        builder.Services.AddTransient<IResend, ResendClient>();

        // Email senders
        builder.Services.AddScoped<ResendEmailSender>();
        builder.Services.AddScoped<SendGridEmailSender>();
        builder.Services.AddScoped<MailKitEmailSender>();
        builder.Services.AddScoped<IEmailSender, EmailSenderDispatcher>();

        builder.Services.AddScoped<INotificationContentBuilder, NotificationContentBuilder>();
        builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        builder.Services
            .AddOptions<NotificationDispatcherOptions>()
            .Bind(builder.Configuration.GetSection("Notifications:Dispatcher"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Notification channels
        builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        builder.Services.AddScoped<INotificationChannel, InAppNotificationChannel>();

        builder.Services.AddQuartz(configurator =>
        {
            var jobKey = new JobKey("StalePendingNotificationsMonitorJob");
            configurator.AddJob<StalePendingNotificationsMonitorJob>(options => options.WithIdentity(jobKey));
            configurator.AddTrigger(options => options
                .ForJob(jobKey)
                .WithIdentity("StalePendingNotificationsMonitorTrigger")
                .WithCronSchedule("0 0 9 * * ?", cron => cron.InTimeZone(TimeZoneInfo.Utc)));
        });

        builder.Services.AddSingleton<NotificationDispatcherHealthState>();
        builder.Services.AddHostedService<NotificationDispatcherHostedService>();

        builder.Services.AddHealthChecks()
            .AddCheck<NotificationDispatcherHealthCheck>("notification-dispatcher")
            .AddCheck<SharedKernel.HealthChecks.DbContextHealthCheck<NotificationsDbContext>>("notifications-db");

        logger.LogInformation("Notifications module services registered");

        return builder;
    }

    /// <summary>
    /// Ensures the notifications database exists and seeds default templates if necessary.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The application instance to support fluent configuration.</returns>
    public static async Task<WebApplication> EnsureNotificationsModuleDatabaseAsync(this WebApplication app)
    {
        await MigrationRunner.RunForModuleAsync<NotificationsDbContext>(
            app.Services,
            app.Lifetime,
            loggerCategory: "NotificationsDatabase",
            seedAsync: SeedNotificationTemplatesAsync);

        return app;
    }

    private static async Task SeedNotificationTemplatesAsync(DbContext db, CancellationToken ct)
    {
        var context = (NotificationsDbContext)db;

        var seedTemplates = new (string Key, string Subject)[]
        {
            ("registration-welcome", "Welcome to {{ProjectName}}, {{FirstName}}!"),
            ("role-assigned", "New role assigned: {{RoleName}}"),
            ("role-revoked", "Role revoked: {{RoleName}}"),
            ("password-reset", "Password reset for {{ProjectName}}"),
            ("profile-updated", "Profile updated")
        };

        foreach (var (key, subject) in seedTemplates)
        {
            var body = EmailTemplateLoader.LoadTemplate(key);
            var existing = await context.NotificationTemplates.FirstOrDefaultAsync(t => t.Key == key, ct);

            if (existing is null)
            {
                context.NotificationTemplates.Add(NotificationTemplate.Create(key, subject, body, true));
            }
            else
            {
                existing.UpdateContent(subject, body, true);
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
