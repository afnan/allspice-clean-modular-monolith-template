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
        builder.AddNpgsqlDbContext<NotificationsDbContext>(DatabaseResourceName);

        builder.Services.AddScoped<INotificationsDbContext, NotificationsDbContext>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        builder.Services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();

        builder.Services.AddMediator();

        builder.Services.AddValidatorsFromAssembly(typeof(AppAssemblyReference).Assembly);

        builder.Services.Configure<SinchOptions>(builder.Configuration.GetSection("Notifications:Sinch"));
        builder.Services.Configure<MailKitSmtpOptions>(builder.Configuration.GetSection("Notifications:Smtp"));

        builder.Services.AddHttpClient<SinchEmailSender>(client =>
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        builder.Services.AddScoped<SinchEmailSender>();
        builder.Services.AddScoped<MailKitEmailSender>();
        builder.Services.AddScoped<IEmailSender, EmailSenderDispatcher>();
        builder.Services.AddScoped<INotificationContentBuilder, NotificationContentBuilder>();
        builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        builder.Services.Configure<NotificationDispatcherOptions>(builder.Configuration.GetSection("Notifications:Dispatcher"));
        builder.Services.PostConfigure<NotificationDispatcherOptions>(options =>
        {
            Guard.Against.NegativeOrZero(options.PollIntervalSeconds, nameof(options.PollIntervalSeconds));
        });

        builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        builder.Services.AddScoped<INotificationChannel, InAppNotificationChannel>();

        builder.Services.AddHttpClient<SinchSmsNotificationChannel>(client =>
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        builder.Services.AddScoped<INotificationChannel, SinchSmsNotificationChannel>();

        builder.Services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<NotificationRequestedIntegrationEventConsumer>();

            configurator.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        builder.Services.AddQuartz(configurator =>
        {
            var jobKey = new JobKey("NotificationDailyDigestJob");
            configurator.AddJob<NotificationDailyDigestJob>(options => options.WithIdentity(jobKey));
            configurator.AddTrigger(options => options
                .ForJob(jobKey)
                .WithIdentity("NotificationDailyDigestTrigger")
                .WithCronSchedule("0 0 9 * * ?", cron => cron.InTimeZone(TimeZoneInfo.Utc)));
        });

        builder.Services.AddHostedService<NotificationDispatcherHostedService>();

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
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await context.Database.EnsureCreatedAsync(app.Lifetime.ApplicationStopping);

        if (!await context.NotificationTemplates.AnyAsync(template => template.Key == "hr.welcome"))
        {
            var template = NotificationTemplate.Create(
                "hr.welcome",
                "Welcome to AllSpice.CleanModularMonolith, {{FirstName}}!",
                "Hi {{FirstName}},<br/>Welcome aboard! We're excited to have you join the team.",
                true);

            context.NotificationTemplates.Add(template);
            await context.SaveChangesAsync();
        }

        return app;
    }
}


