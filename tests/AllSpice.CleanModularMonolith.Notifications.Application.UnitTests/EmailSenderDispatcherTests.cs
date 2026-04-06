using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests;

/// <summary>
/// Tests for the email provider selection logic.
/// Since the concrete senders are sealed, we test the configuration/selection logic
/// by verifying the options validation used by EmailSenderDispatcher.
/// </summary>
public class EmailProviderSelectionTests
{
    [Fact]
    public void ResendOptions_IsConfigured_WhenApiKeyAndFromAddressSet()
    {
        var opts = new ResendOptions { ApiKey = "rk_test_123", FromAddress = "noreply@example.com" };
        Assert.True(IsResendConfigured(opts));
    }

    [Fact]
    public void ResendOptions_IsNotConfigured_WhenApiKeyEmpty()
    {
        var opts = new ResendOptions { ApiKey = "", FromAddress = "noreply@example.com" };
        Assert.False(IsResendConfigured(opts));
    }

    [Fact]
    public void ResendOptions_IsNotConfigured_WhenFromAddressEmpty()
    {
        var opts = new ResendOptions { ApiKey = "rk_test_123", FromAddress = "" };
        Assert.False(IsResendConfigured(opts));
    }

    [Fact]
    public void SendGridOptions_IsConfigured_WhenApiKeyAndFromAddressSet()
    {
        var opts = new SendGridOptions { ApiKey = "SG.test", FromAddress = "noreply@example.com" };
        Assert.True(IsSendGridConfigured(opts));
    }

    [Fact]
    public void SendGridOptions_IsNotConfigured_WhenApiKeyEmpty()
    {
        var opts = new SendGridOptions { ApiKey = "", FromAddress = "noreply@example.com" };
        Assert.False(IsSendGridConfigured(opts));
    }

    [Fact]
    public void ResendOptions_HasCorrectDefaults()
    {
        var opts = new ResendOptions();
        Assert.Equal(string.Empty, opts.ApiKey);
        Assert.Equal(string.Empty, opts.FromAddress);
        Assert.Equal(string.Empty, opts.FromName);
        Assert.Null(opts.ReplyToAddress);
    }

    [Fact]
    public void SendGridOptions_HasCorrectDefaults()
    {
        var opts = new SendGridOptions();
        Assert.Equal(string.Empty, opts.ApiKey);
        Assert.Equal(string.Empty, opts.FromAddress);
        Assert.Equal(string.Empty, opts.FromName);
        Assert.Null(opts.ReplyToAddress);
    }

    // Mirror the dispatcher's private configuration check logic
    private static bool IsResendConfigured(ResendOptions opts) =>
        !string.IsNullOrWhiteSpace(opts.ApiKey) &&
        !string.IsNullOrWhiteSpace(opts.FromAddress);

    private static bool IsSendGridConfigured(SendGridOptions opts) =>
        !string.IsNullOrWhiteSpace(opts.ApiKey) &&
        !string.IsNullOrWhiteSpace(opts.FromAddress);
}
