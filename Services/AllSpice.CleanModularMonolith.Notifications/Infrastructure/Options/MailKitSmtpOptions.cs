namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

public sealed class MailKitSmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseSsl { get; set; } = false;

    public string? Username { get; set; }
        = null;

    public string? Password { get; set; }
        = null;

    public string FromAddress { get; set; } = "no-reply@AllSpice.CleanModularMonolith.local";

    public string FromName { get; set; } = "AllSpice.CleanModularMonolith Notifications";
}
