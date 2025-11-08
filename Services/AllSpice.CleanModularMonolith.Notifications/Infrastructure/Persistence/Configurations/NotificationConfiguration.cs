using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Channel)
            .HasConversion(channel => channel.Name, value => NotificationChannel.FromName(value, ignoreCase: true))
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(notification => notification.Status)
            .HasConversion(status => status.Name, value => NotificationStatus.FromName(value, ignoreCase: true))
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(notification => notification.Subject)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(notification => notification.Body)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(notification => notification.TemplateKey)
            .HasMaxLength(150);

        builder.Property(notification => notification.MetadataJson)
            .HasColumnType("text");

        builder.Property(notification => notification.CorrelationId)
            .HasMaxLength(100);

        builder.Property(notification => notification.AttemptCount)
            .IsRequired();

        builder.Property(notification => notification.LastAttemptedUtc);

        builder.Property(notification => notification.NextAttemptUtc);

        builder.Property(notification => notification.LastError)
            .HasMaxLength(512);

        builder.OwnsOne(notification => notification.Recipient, ownedNavigationBuilder =>
        {
            ownedNavigationBuilder.Property(recipient => recipient.UserId)
                .HasMaxLength(64)
                .IsRequired();

            ownedNavigationBuilder.Property(recipient => recipient.Email)
                .HasMaxLength(256);

            ownedNavigationBuilder.Property(recipient => recipient.PhoneNumber)
                .HasMaxLength(64);

            ownedNavigationBuilder.ToTable("NotificationRecipients");
        });

        builder.HasIndex(notification => new { notification.Channel, notification.Status });
        builder.HasIndex(notification => notification.CreatedUtc);
        builder.HasIndex(notification => notification.CorrelationId);
    }
}


