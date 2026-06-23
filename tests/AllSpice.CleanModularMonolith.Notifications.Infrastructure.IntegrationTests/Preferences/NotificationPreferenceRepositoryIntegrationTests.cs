using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Preferences;

public class NotificationPreferenceRepositoryIntegrationTests
{
    private static readonly Guid SampleUserId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task GetByUserAndChannelAsync_ReturnsStoredPreference()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationPreferenceRepository(database.Context);

        var preference = NotificationPreference.Create(SampleUserId, NotificationChannel.InApp, isEnabled: true);
        await repository.AddAsync(preference, CancellationToken.None);

        var retrieved = await repository.GetByUserAndChannelAsync(SampleUserId, NotificationChannel.InApp, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(preference.Id, retrieved!.Id);
        Assert.True(retrieved.IsEnabled);
    }

    [Fact]
    public async Task GetByUserAndChannelAsync_ReturnsNull_WhenNotFound()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationPreferenceRepository(database.Context);

        var result = await repository.GetByUserAndChannelAsync(SampleUserId, NotificationChannel.Email, CancellationToken.None);

        Assert.Null(result);
    }
}
