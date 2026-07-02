using System.Text.Json;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests.Notifications;

public class NotificationContentBuilderTests
{
    private static NotificationContentBuilder BuildSut() =>
        new(new Mock<INotificationTemplateRepository>().Object, NullLogger<NotificationContentBuilder>.Instance);

    private static Notification QueuedWith(string subject, string? metadataJson) => Notification.Queue(
        NotificationChannel.Email,
        NotificationRecipient.Create(string.Empty, "user@example.com", null),
        subject,
        "Body",
        templateKey: null,
        metadataJson: metadataJson,
        nowUtc: DateTimeOffset.UtcNow);

    [Fact]
    public async Task BuildAsync_strips_crlf_from_subject()
    {
        var notification = QueuedWith("Hello\r\nBcc: evil@example.com", metadataJson: null);

        var result = await BuildSut().BuildAsync(notification);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain('\r', result.Value.Subject);
        Assert.DoesNotContain('\n', result.Value.Subject);
    }

    [Theory]
    [InlineData("Hello\rBcc: evil")]
    [InlineData("Hello\nBcc: evil")]
    public async Task BuildAsync_strips_lone_cr_or_lf_from_subject(string rawSubject)
    {
        var result = await BuildSut().BuildAsync(QueuedWith(rawSubject, metadataJson: null));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain('\r', result.Value.Subject);
        Assert.DoesNotContain('\n', result.Value.Subject);
    }

    [Fact]
    public async Task BuildAsync_drops_protocol_relative_action_url()
    {
        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["ActionUrl"] = "//evil.com/x" });
        var result = await BuildSut().BuildAsync(QueuedWith("Subject", metadataJson));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("evil.com", result.Value.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_drops_non_http_action_url()
    {
        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["ActionUrl"] = "javascript:alert(1)" });
        var notification = QueuedWith("Subject", metadataJson);

        var result = await BuildSut().BuildAsync(notification);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("javascript:", result.Value.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_keeps_http_action_url()
    {
        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["ActionUrl"] = "https://example.com/accept" });
        var notification = QueuedWith("Subject", metadataJson);

        var result = await BuildSut().BuildAsync(notification);

        Assert.True(result.IsSuccess);
        Assert.Contains("https://example.com/accept", result.Value.Body);
    }

    [Fact]
    public async Task BuildAsync_html_encodes_fallback_body_when_template_row_missing()
    {
        // TemplateKey is set but the template row is absent → fallback path. The stored body must be HTML-encoded
        // (wrapped in the branded layout) exactly like the no-template path, not emitted raw with isHtml=true
        // (an HTML-injection/XSS vector). The mocked repository returns null for GetByKeyAsync.
        var notification = Notification.Queue(
            NotificationChannel.Email,
            NotificationRecipient.Create(string.Empty, "user@example.com", null),
            "Subject",
            "<script>alert('xss')</script>",
            templateKey: "does-not-exist",
            metadataJson: null,
            nowUtc: DateTimeOffset.UtcNow);

        var result = await BuildSut().BuildAsync(notification);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("<script>", result.Value.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", result.Value.Body, StringComparison.OrdinalIgnoreCase);
    }
}
