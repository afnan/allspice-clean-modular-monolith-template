namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

/// <summary>
/// Configuration object for Sinch SMS and email integrations.
/// </summary>
public sealed class SinchOptions
{
    /// <summary>Gets or sets the Sinch project identifier.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Sinch API key used for authentication.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets SMS-specific Sinch settings.</summary>
    public SmsOptions Sms { get; set; } = new();

    /// <summary>Gets or sets email-specific Sinch settings.</summary>
    public EmailOptions Email { get; set; } = new();

    /// <summary>
    /// Sinch SMS options.
    /// </summary>
    public sealed class SmsOptions
    {
        /// <summary>Gets or sets the Sinch SMS service plan identifier.</summary>
        public string ServicePlanId { get; set; } = string.Empty;

        /// <summary>Gets or sets the default origin phone number for outgoing SMS messages.</summary>
        public string FromNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sinch email options.
    /// </summary>
    public sealed class EmailOptions
    {
        /// <summary>Gets or sets the default from address used with Sinch email.</summary>
        public string FromAddress { get; set; } = string.Empty;

        /// <summary>Gets or sets the reply-to address for Sinch email.</summary>
        public string ReplyToAddress { get; set; } = string.Empty;
    }
}
