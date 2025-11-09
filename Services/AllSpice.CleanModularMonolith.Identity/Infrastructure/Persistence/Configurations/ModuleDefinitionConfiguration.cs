using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class ModuleDefinitionConfiguration : IEntityTypeConfiguration<ModuleDefinition>
{
    public void Configure(EntityTypeBuilder<ModuleDefinition> builder)
    {
        builder.ToTable("ModuleDefinitions");

        builder.HasKey(module => module.Id);

        builder.Property(module => module.Key)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(module => module.DisplayName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(module => module.Description)
            .HasMaxLength(512);

        builder.Property(module => module.CreatedUtc)
            .IsRequired();

        builder.OwnsMany(module => module.Roles, ownedBuilder =>
        {
            ownedBuilder.ToTable("ModuleRoles");
            ownedBuilder.WithOwner().HasForeignKey("ModuleId");
            ownedBuilder.HasKey("ModuleId", "RoleKey");

            ownedBuilder.Property(role => role.RoleKey)
                .HasColumnName("RoleKey")
                .HasMaxLength(64)
                .IsRequired();

            ownedBuilder.Property(role => role.Name)
                .HasMaxLength(128)
                .IsRequired();

            ownedBuilder.Property(role => role.Description)
                .HasMaxLength(512);
        });

        builder.HasIndex(module => module.Key)
            .IsUnique();
    }
}


