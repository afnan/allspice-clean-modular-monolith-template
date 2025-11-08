using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");

        builder.HasKey(preference => preference.Id);

        builder.Property(preference => preference.UserId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(preference => preference.Channel)
            .HasConversion(channel => channel.Name, value => NotificationChannel.FromName(value, ignoreCase: true))
            .HasMaxLength(25)
            .IsRequired();

        builder.HasIndex(preference => new { preference.UserId, preference.Channel })
            .IsUnique();
    }
}


