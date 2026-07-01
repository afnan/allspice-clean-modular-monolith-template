using System.Text.RegularExpressions;
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
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Resolves an <see cref="EmailEnvelope"/> from the message and the provider's configured defaults:
    /// the message's own From wins over <paramref name="defaultFromAddress"/>. An HTML body is placed in
    /// <see cref="HtmlBody"/> AND accompanied by a generated plain-text alternative in <see cref="TextBody"/>
    /// (so the MimeMessage is a proper multipart/alternative — a missing plain-text part hurts deliverability
    /// and spam scoring); a plain-text body is placed in <see cref="TextBody"/> only.
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
            TextBody: message.IsHtml ? ToPlainText(message.Body) : message.Body);

    /// <summary>
    /// Produces a simple plain-text alternative from an HTML body: strip tags, decode HTML entities, and
    /// collapse runs of whitespace. Not a full HTML-to-text renderer — just enough to give clients a readable
    /// text/plain part alongside the HTML.
    /// </summary>
    private static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var withoutTags = TagRegex.Replace(html, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }
}
