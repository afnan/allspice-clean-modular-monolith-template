using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

internal sealed class ModuleRoleTemplateConfiguration : IEntityTypeConfiguration<ModuleRoleTemplate>
{
    public void Configure(EntityTypeBuilder<ModuleRoleTemplate> builder)
    {
        builder.ToTable("ModuleRoleTemplates");

        builder.HasKey(template => template.Id);

        builder.HasIndex(template => template.TemplateKey)
            .IsUnique();

        builder.Property(template => template.TemplateKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(template => template.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(template => template.Description)
            .HasMaxLength(1024);

        builder.OwnsMany(template => template.Roles, roles =>
        {
            roles.ToTable("ModuleRoleTemplateRoles");

            roles.WithOwner().HasForeignKey("TemplateId");

            roles.HasKey("TemplateId", "ModuleKey", "RoleKey");

            roles.Property(role => role.ModuleKey)
                .HasMaxLength(64)
                .IsRequired();

            roles.Property(role => role.RoleKey)
                .HasMaxLength(64)
                .IsRequired();
        });
    }
}


