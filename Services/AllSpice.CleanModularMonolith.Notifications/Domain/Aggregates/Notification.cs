using System.Collections.Generic;
using System.Text.Json;
using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

public sealed class Notification : Entity, IAggregateRoot
{
    internal const int MaxDeliveryAttempts = 5;

    private Notification()
    {
    }

    private Notification(
        NotificationChannel channel,
        NotificationRecipient recipient,
        string subject,
        string body,
        string? templateKey,
        string? metadataJson,
        DateTimeOffset nowUtc,
        DateTimeOffset? scheduledSendUtc,
        string? correlationId)
    {
        Id = Guid.NewGuid();
        Channel = channel;
        Recipient = recipient;
        Subject = subject ?? string.Empty;
        Body = body ?? string.Empty;
        TemplateKey = templateKey;
        MetadataJson = metadataJson;
        Status = NotificationStatus.Pending;
        ScheduledSendUtc = scheduledSendUtc;
        CorrelationId = correlationId;
        CreatedUtc = nowUtc;

        RegisterDomainEvent(new NotificationQueuedDomainEvent(Id, recipient.UserId, channel.Name, nowUtc));
    }

    public NotificationChannel Channel { get; private set; } = null!;

    public NotificationRecipient Recipient { get; private set; } = null!;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? TemplateKey { get; private set; }
        = null;

    public string? MetadataJson { get; private set; }
        = null;

    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? ScheduledSendUtc { get; private set; }
        = null;

    public DateTimeOffset? LastUpdatedUtc { get; private set; }
        = null;

    public DateTimeOffset? LastAttemptedUtc { get; private set; }
        = null;

    public DateTimeOffset? NextAttemptUtc { get; private set; }
        = null;

    public int AttemptCount { get; private set; }
        = 0;

    public string? LastError { get; private set; }
        = null;

    public string? CorrelationId { get; private set; }
        = null;


    public DateTimeOffset? ReadAt { get; private set; }
        = null;

    public static Notification Queue(
        NotificationChannel channel,
        NotificationRecipient recipient,
        string subject,
        string body,
        string? templateKey,
        string? metadataJson,
        DateTimeOffset nowUtc,
        DateTimeOffset? scheduledSendUtc = null,
        string? correlationId = null)
    {
        Guard.Against.Null(channel, nameof(channel));
        Guard.Against.Null(recipient, nameof(recipient));

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
            Guard.Against.NullOrWhiteSpace(body, nameof(body));
        }

        return new Notification(channel, recipient, subject, body, templateKey, metadataJson, nowUtc, scheduledSendUtc, correlationId);
    }

    public bool IsReadyToDispatch(DateTimeOffset utcNow)
    {
        if (Status != NotificationStatus.Pending)
        {
            return false;
        }

        var dueAt = ScheduledSendUtc ?? CreatedUtc;

        if (NextAttemptUtc.HasValue && NextAttemptUtc.Value > dueAt)
        {
            dueAt = NextAttemptUtc.Value;
        }

        return dueAt <= utcNow;
    }

    public void RecordAttempt(DateTimeOffset nowUtc)
    {
        AttemptCount += 1;
        LastAttemptedUtc = nowUtc;
        LastUpdatedUtc = LastAttemptedUtc;
    }

    public void MarkDispatched(DateTimeOffset nowUtc)
    {
        Status = NotificationStatus.Dispatched;
        LastUpdatedUtc = nowUtc;
    }

    public void MarkDelivered(DateTimeOffset nowUtc)
    {
        Status = NotificationStatus.Delivered;
        LastUpdatedUtc = nowUtc;
        LastError = null;
        NextAttemptUtc = null;
    }

    public void HandleFailure(string error, DateTimeOffset nowUtc)
    {
        LastError = error;
        LastUpdatedUtc = nowUtc;

        if (AttemptCount >= MaxDeliveryAttempts)
        {
            Status = NotificationStatus.Failed;
            NextAttemptUtc = null;
            return;
        }

        Status = NotificationStatus.Pending;
        NextAttemptUtc = CalculateBackoff(AttemptCount, nowUtc);
    }

    public void Cancel(DateTimeOffset nowUtc, string? reason = null)
    {
        Status = NotificationStatus.Cancelled;
        LastUpdatedUtc = nowUtc;
        LastError = reason;
        NextAttemptUtc = null;
    }

    public void MarkAsRead(DateTimeOffset nowUtc)
    {
        if (ReadAt.HasValue) return;
        ReadAt = nowUtc;
        LastUpdatedUtc = ReadAt;
    }

    public IReadOnlyDictionary<string, string> GetMetadata()
    {
        if (string.IsNullOrWhiteSpace(MetadataJson))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson) ?? new Dictionary<string, string>();
    }

    private static DateTimeOffset CalculateBackoff(int attemptCount, DateTimeOffset nowUtc)
    {
        var seconds = Math.Pow(2, attemptCount); // exponential backoff
        var maxDelay = TimeSpan.FromMinutes(15);
        var delay = TimeSpan.FromSeconds(Math.Min(seconds, maxDelay.TotalSeconds));
        return nowUtc.Add(delay);
    }
}


