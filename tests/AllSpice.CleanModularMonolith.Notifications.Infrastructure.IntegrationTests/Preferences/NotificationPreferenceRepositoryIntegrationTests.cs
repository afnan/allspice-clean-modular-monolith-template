using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Preferences;

public class NotificationPreferenceRepositoryIntegrationTests
{
    [Fact]
    public async Task GetByUserAndChannelAsync_ReturnsStoredPreference()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationPreferenceRepository(database.Context);

        var preference = NotificationPreference.Create("user-123", NotificationChannel.InApp, isEnabled: true);
        await repository.AddAsync(preference, CancellationToken.None);

        var retrieved = await repository.GetByUserAndChannelAsync("user-123", NotificationChannel.InApp, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(preference.Id, retrieved!.Id);
        Assert.True(retrieved.IsEnabled);
    }

    [Fact]
    public async Task GetByUserAndChannelAsync_ReturnsNull_WhenNotFound()
    {
        await using var database = await TestSqliteDatabase.CreateAsync();
        var repository = new NotificationPreferenceRepository(database.Context);

        var result = await repository.GetByUserAndChannelAsync("user-123", NotificationChannel.Email, CancellationToken.None);

        Assert.Null(result);
    }
}


