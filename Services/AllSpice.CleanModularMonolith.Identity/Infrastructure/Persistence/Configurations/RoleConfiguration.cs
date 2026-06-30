using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("authz_roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(500);
        // Role.Key is normalized to lowercase in the domain (Role.Create), so the unique index enforces
        // case-insensitive uniqueness without requiring a CI collation on the index itself (ADR-0008).
        builder.HasIndex(r => r.Key).IsUnique();
    }
}
