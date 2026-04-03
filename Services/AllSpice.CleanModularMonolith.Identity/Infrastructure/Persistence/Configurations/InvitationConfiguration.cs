using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.FirstName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.LastName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.Role)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.Token)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion(
                s => s.Value,
                v => InvitationStatus.FromValue(v))
            .IsRequired();

        builder.Property(i => i.ExpiresAtUtc)
            .IsRequired();

        builder.Property(i => i.CreatedByUserId)
            .HasMaxLength(256);

        builder.Property(i => i.KeycloakUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.LocalUserId)
            .IsRequired();

        builder.Property(i => i.CreatedOnUtc)
            .IsRequired();

        builder.Property(i => i.LastModifiedOnUtc);

        builder.HasIndex(i => i.Email);
        builder.HasIndex(i => i.Token).IsUnique();

        builder.Ignore(i => i.DomainEvents);
    }
}
