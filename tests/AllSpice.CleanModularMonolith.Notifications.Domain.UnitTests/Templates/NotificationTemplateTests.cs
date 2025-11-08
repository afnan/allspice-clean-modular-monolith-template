using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Templates;

public class NotificationTemplateTests
{
    [Fact]
    public void Create_TrimsKey()
    {
        var template = NotificationTemplate.Create(" welcome ", "Subject", "Body", true);

        Assert.Equal("welcome", template.Key);
    }

    [Fact]
    public void UpdateContent_ReplacesTemplates()
    {
        var template = NotificationTemplate.Create("welcome", "Subject", "Body", true);

        template.UpdateContent("New subject", "New body", false);

        Assert.Equal("New subject", template.SubjectTemplate);
        Assert.Equal("New body", template.BodyTemplate);
        Assert.False(template.IsHtml);
    }
}


