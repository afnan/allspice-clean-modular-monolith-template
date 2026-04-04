using System.Text.RegularExpressions;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Builds rendered notification content from stored templates and metadata.
/// HTML-encodes token values in HTML templates to prevent XSS.
/// Wraps plain-text emails in a branded HTML layout when no template is specified.
/// </summary>
public sealed class NotificationContentBuilder : INotificationContentBuilder
{
    private static readonly Regex TokenRegex = new(@"{{(?<key>[^}]+)}}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ILogger<NotificationContentBuilder> _logger;

    public NotificationContentBuilder(
        INotificationTemplateRepository templateRepository,
        ILogger<NotificationContentBuilder> logger)
    {
        _templateRepository = templateRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<NotificationContent>> BuildAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.TemplateKey))
        {
            var body = WrapInBrandedLayout(notification.Body, notification.GetMetadata());
            return Result.Success(new NotificationContent(notification.Subject, body, true));
        }

        var template = await _templateRepository.GetByKeyAsync(notification.TemplateKey, cancellationToken);

        if (template is null)
        {
            _logger.LogWarning("Notification template '{TemplateKey}' not found; falling back to stored content.", notification.TemplateKey);
            return Result.Success(new NotificationContent(notification.Subject, notification.Body, true));
        }

        var metadata = notification.GetMetadata();

        var subject = ReplaceTokens(template.SubjectTemplate, metadata, htmlEncode: false);
        var body2 = ReplaceTokens(template.BodyTemplate, metadata, htmlEncode: template.IsHtml);

        return Result.Success(new NotificationContent(subject, body2, template.IsHtml));
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> metadata, bool htmlEncode)
    {
        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (!metadata.TryGetValue(key, out var value)) return match.Value;
            return htmlEncode ? System.Net.WebUtility.HtmlEncode(value) : value;
        });
    }

    private static string WrapInBrandedLayout(string body, IReadOnlyDictionary<string, string> metadata)
    {
        var firstName = metadata.TryGetValue("FirstName", out var fn)
            ? System.Net.WebUtility.HtmlEncode(fn)
            : null;
        var actionUrl = metadata.TryGetValue("ActionUrl", out var url) ? url : null;
        var encodedBody = System.Net.WebUtility.HtmlEncode(body);

        var contentHtml = "<div style=\"font-size:15px;line-height:1.6;color:#374151;\">";
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            contentHtml += $"<p style=\"margin:0 0 16px;\">Hi {firstName},</p>";
        }
        contentHtml += $"<p style=\"margin:0 0 16px;\">{encodedBody}</p>";
        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            contentHtml += $"<p style=\"margin:24px 0 0;\"><a href=\"{System.Net.WebUtility.HtmlEncode(actionUrl)}\" style=\"display:inline-block;padding:10px 24px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;\">View</a></p>";
        }
        contentHtml += "</div>";

        try
        {
            var layout = EmailTemplateLoader.LoadRawTemplate("_Layout");
            return layout.Replace("{{content}}", contentHtml);
        }
        catch
        {
            return contentHtml;
        }
    }
}
