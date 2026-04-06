namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string? ReplyToAddress { get; set; }
}
