using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");

        builder.HasKey(template => template.Id);

        builder.Property(template => template.Key)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(template => template.Key)
            .IsUnique();

        builder.Property(template => template.SubjectTemplate)
            .IsRequired();

        builder.Property(template => template.BodyTemplate)
            .IsRequired();
    }
}


