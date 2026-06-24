using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests;

/// <summary>
/// D8 regression: in a non-Development environment the dispatcher must fail fast when no real email
/// provider is configured, instead of silently sending via MailKit/localhost (which drops the mail).
/// </summary>
public class EmailSenderDispatcherFailFastTests
{
    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task Production_with_no_provider_configured_fails_fast()
    {
        var dispatcher = new EmailSenderDispatcher(
            new StubEnvironment(),
            // Senders are never invoked on the no-provider-configured path — the guard throws first.
            resendSender: null!,
            sendGridSender: null!,
            mailKitSender: null!,
            Options.Create(new ResendOptions()),     // empty -> not configured
            Options.Create(new SendGridOptions()),   // empty -> not configured
            NullLogger<EmailSenderDispatcher>.Instance);

        var message = new EmailMessage("user@example.com", "Subject", "Body");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendEmailAsync(message));

        Assert.Contains("No production email provider is configured", ex.Message);
    }
}
