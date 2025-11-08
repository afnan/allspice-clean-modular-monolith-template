using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

public sealed class NotificationContentBuilder : INotificationContentBuilder
{
    private static readonly Regex TokenRegex = new Regex(@"{{(?<key>[^}]+)}}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ILogger<NotificationContentBuilder> _logger;

    public NotificationContentBuilder(
        INotificationTemplateRepository templateRepository,
        ILogger<NotificationContentBuilder> logger)
    {
        _templateRepository = templateRepository;
        _logger = logger;
    }

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

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> metadata)
    {
        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return metadata.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}


