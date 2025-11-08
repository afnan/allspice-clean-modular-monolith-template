using System.Net.Http.Headers;
using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Jobs;
using FluentValidation;
using MassTransit;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AppAssemblyReference = AllSpice.CleanModularMonolith.Notifications.Application.AssemblyReference;
using Quartz;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Extensions;

public static class NotificationsModuleExtensions
{
    private const string DatabaseResourceName = "notificationsdb";

    public static IHostApplicationBuilder AddNotificationsModuleServices(
        this IHostApplicationBuilder builder,
        ILogger logger)
    {
        builder.AddNpgsqlDbContext<NotificationsDbContext>(DatabaseResourceName);

        builder.Services.AddScoped<INotificationsDbContext, NotificationsDbContext>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        builder.Services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();

        builder.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

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

    public static async Task<WebApplication> EnsureNotificationsModuleDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await context.Database.EnsureCreatedAsync();

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


