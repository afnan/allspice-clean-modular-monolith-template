namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

public sealed class SinchOptions
{
    public string ProjectId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public SmsOptions Sms { get; set; } = new();

    public EmailOptions Email { get; set; } = new();

    public sealed class SmsOptions
    {
        public string ServicePlanId { get; set; } = string.Empty;

        public string FromNumber { get; set; } = string.Empty;
    }

    public sealed class EmailOptions
    {
        public string FromAddress { get; set; } = string.Empty;

        public string ReplyToAddress { get; set; } = string.Empty;
    }
}
