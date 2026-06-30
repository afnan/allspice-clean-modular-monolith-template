using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("authz_role_permissions");
        builder.HasKey(rp => rp.Id);
        builder.Property(rp => rp.RoleId).IsRequired();
        builder.Property(rp => rp.PermissionId).IsRequired();
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        builder.HasIndex(rp => rp.RoleId);
    }
}
