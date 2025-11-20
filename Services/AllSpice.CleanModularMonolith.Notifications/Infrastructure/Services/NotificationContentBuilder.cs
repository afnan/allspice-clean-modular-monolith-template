using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Builds rendered notification content from stored templates and metadata.
/// </summary>
public sealed class NotificationContentBuilder : INotificationContentBuilder
{
    private static readonly Regex TokenRegex = new Regex(@"{{(?<key>[^}]+)}}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ILogger<NotificationContentBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationContentBuilder"/> class.
    /// </summary>
    /// <param name="templateRepository">Repository used to load notification templates.</param>
    /// <param name="logger">Logger used to trace template usage.</param>
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
            return Result.Success(new NotificationContent(notification.Subject, notification.Body, true));
        }

        var template = await _templateRepository.GetByKeyAsync(notification.TemplateKey, cancellationToken);

        if (template is null)
        {
            _logger.LogWarning("Notification template '{TemplateKey}' not found; falling back to stored content.", notification.TemplateKey);
            return Result.Success(new NotificationContent(notification.Subject, notification.Body, true));
        }

        var metadata = notification.GetMetadata();

        var subject = ReplaceTokens(template.SubjectTemplate, metadata);
        var body = ReplaceTokens(template.BodyTemplate, metadata);

        return Result.Success(new NotificationContent(subject, body, template.IsHtml));
    }

    /// <summary>
    /// Replaces template tokens with metadata values.
    /// </summary>
    /// <param name="template">Template text containing <c>{{token}}</c> placeholders.</param>
    /// <param name="metadata">Key/value metadata collected from the notification.</param>
    /// <returns>The rendered template string.</returns>
    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> metadata)
    {
        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return metadata.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}


