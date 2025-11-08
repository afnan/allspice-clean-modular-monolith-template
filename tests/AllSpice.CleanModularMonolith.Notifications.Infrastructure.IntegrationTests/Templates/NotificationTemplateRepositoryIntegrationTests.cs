using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Templates;

public class NotificationTemplateRepositoryIntegrationTests
{
    [Fact]
    public async Task GetByKeyAsync_ReturnsTemplate()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationTemplateRepository(database.Context);

        var template = NotificationTemplate.Create("welcome", "Subject", "Body", isHtml: true);
        await repository.AddAsync(template, CancellationToken.None);

        var retrieved = await repository.GetByKeyAsync("welcome", CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(template.Id, retrieved!.Id);
        Assert.Equal("Subject", retrieved.SubjectTemplate);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsNull_WhenMissing()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationTemplateRepository(database.Context);

        var result = await repository.GetByKeyAsync("does-not-exist", CancellationToken.None);

        Assert.Null(result);
    }
}


