using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

/// <summary>
/// Provider-agnostic resolved view of an outbound email: the effective From, optional display name and
/// reply-to, and the body routed to exactly one of HTML/text. Centralises the From-fallback and
/// HTML-vs-text selection that the MailKit/Resend/SendGrid senders otherwise each re-implement; each
/// sender then only maps these resolved fields onto its own SDK types.
/// </summary>
public sealed record EmailEnvelope(
    string FromAddress,
    string? FromName,
    string? ReplyToAddress,
    string? HtmlBody,
    string? TextBody)
{
    /// <summary>
    /// Resolves an <see cref="EmailEnvelope"/> from the message and the provider's configured defaults:
    /// the message's own From wins over <paramref name="defaultFromAddress"/>, and the body is placed in
    /// <see cref="HtmlBody"/> or <see cref="TextBody"/> according to <see cref="EmailMessage.IsHtml"/>.
    /// </summary>
    public static EmailEnvelope From(
        EmailMessage message,
        string defaultFromAddress,
        string? fromName,
        string? replyToAddress) =>
        new(
            message.From ?? defaultFromAddress,
            fromName,
            replyToAddress,
            HtmlBody: message.IsHtml ? message.Body : null,
            TextBody: message.IsHtml ? null : message.Body);
}
