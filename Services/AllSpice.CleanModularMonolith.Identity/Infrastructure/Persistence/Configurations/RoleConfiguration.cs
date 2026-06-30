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
        // Case-insensitive uniqueness mirrors the resolver's case-insensitive match (ADR-0008).
        builder.HasIndex(r => r.Key).IsUnique();
    }
}
