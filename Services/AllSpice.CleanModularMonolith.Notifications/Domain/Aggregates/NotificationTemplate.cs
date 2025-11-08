using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

public sealed class NotificationTemplate : AggregateRoot
{
    private NotificationTemplate()
    {
    }

    private NotificationTemplate(string key, string subjectTemplate, string bodyTemplate, bool isHtml)
    {
        Id = Guid.NewGuid();
        Key = key;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        IsHtml = isHtml;
        CreatedUtc = DateTimeOffset.UtcNow;
        UpdatedUtc = CreatedUtc;
    }

    public string Key { get; private set; } = string.Empty;

    public string SubjectTemplate { get; private set; } = string.Empty;

    public string BodyTemplate { get; private set; } = string.Empty;

    public bool IsHtml { get; private set; }
        = true;

    public DateTimeOffset CreatedUtc { get; private set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; private set; }
        = DateTimeOffset.UtcNow;

    public static NotificationTemplate Create(string key, string subjectTemplate, string bodyTemplate, bool isHtml)
    {
        Guard.Against.NullOrWhiteSpace(key, nameof(key));
        Guard.Against.NullOrWhiteSpace(subjectTemplate, nameof(subjectTemplate));
        Guard.Against.NullOrWhiteSpace(bodyTemplate, nameof(bodyTemplate));

        return new NotificationTemplate(key.Trim(), subjectTemplate, bodyTemplate, isHtml);
    }

    public void UpdateContent(string subjectTemplate, string bodyTemplate, bool isHtml)
    {
        Guard.Against.NullOrWhiteSpace(subjectTemplate, nameof(subjectTemplate));
        Guard.Against.NullOrWhiteSpace(bodyTemplate, nameof(bodyTemplate));

        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        IsHtml = isHtml;
        UpdatedUtc = DateTimeOffset.UtcNow;
    }
}


