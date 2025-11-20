namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

/// <summary>
/// Configuration for MailKit-based SMTP email delivery.
/// </summary>
public sealed class MailKitSmtpOptions
{
    /// <summary>Gets or sets the SMTP host name.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the SMTP port.</summary>
    public int Port { get; set; } = 1025;

    /// <summary>Gets or sets a value indicating whether SSL/TLS should be used.</summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>Gets or sets the SMTP username, if authentication is required.</summary>
    public string? Username { get; set; }
        = null;

    /// <summary>Gets or sets the SMTP password when authentication is required.</summary>
    public string? Password { get; set; }
        = null;

    /// <summary>Gets or sets the default email address used in the From header.</summary>
    public string FromAddress { get; set; } = "no-reply@AllSpice.CleanModularMonolith.local";

    /// <summary>Gets or sets the display name used in the From header.</summary>
    public string FromName { get; set; } = "AllSpice.CleanModularMonolith Notifications";
}
