using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.OwnsOne(u => u.ExternalId, owned =>
        {
            owned.Property(e => e.Value)
                .HasColumnName("ExternalId")
                .HasMaxLength(256)
                .IsRequired();

            owned.HasIndex(e => e.Value)
                .IsUnique();
        });

        builder.Property(u => u.Username)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256);

        builder.Property(u => u.FirstName)
            .HasMaxLength(128);

        builder.Property(u => u.LastName)
            .HasMaxLength(128);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.LastSyncedUtc)
            .IsRequired();

        builder.Property(u => u.CreatedOnUtc)
            .IsRequired();

        builder.Property(u => u.LastModifiedOnUtc);

        builder.HasIndex(u => u.Email);

        builder.Ignore(u => u.DisplayName);
        builder.Ignore(u => u.DomainEvents);
    }
}
