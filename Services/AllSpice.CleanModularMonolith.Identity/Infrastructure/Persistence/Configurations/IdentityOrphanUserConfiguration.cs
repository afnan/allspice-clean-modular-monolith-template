using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

internal sealed class IdentityOrphanUserConfiguration : IEntityTypeConfiguration<IdentityOrphanUser>
{
    public void Configure(EntityTypeBuilder<IdentityOrphanUser> builder)
    {
        builder.ToTable("IdentityOrphanUsers");

        builder.HasKey(orphan => orphan.Id);

        builder.Property(orphan => orphan.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(orphan => orphan.Username)
            .HasMaxLength(256);

        builder.Property(orphan => orphan.Email)
            .HasMaxLength(256);

        builder.Property(orphan => orphan.DisplayName)
            .HasMaxLength(256);

        builder.HasIndex(orphan => orphan.UserId)
            .IsUnique();
    }
}


