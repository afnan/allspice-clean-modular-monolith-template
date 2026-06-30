using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("authz_permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Key).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(500);
        builder.Property(p => p.IsSystem).IsRequired();
        builder.HasIndex(p => p.Key).IsUnique();
    }
}
