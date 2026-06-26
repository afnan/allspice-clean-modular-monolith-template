using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Templates;

public class NotificationTemplateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsKey()
    {
        var template = NotificationTemplate.Create(" welcome ", "Subject", "Body", true, Now);

        Assert.Equal("welcome", template.Key);
        Assert.Equal(Now, template.CreatedUtc);
    }

    [Fact]
    public void UpdateContent_ReplacesTemplates()
    {
        var template = NotificationTemplate.Create("welcome", "Subject", "Body", true, Now);

        var later = Now.AddMinutes(5);
        template.UpdateContent("New subject", "New body", false, later);

        Assert.Equal("New subject", template.SubjectTemplate);
        Assert.Equal("New body", template.BodyTemplate);
        Assert.False(template.IsHtml);
        Assert.Equal(later, template.UpdatedUtc);
    }
}


