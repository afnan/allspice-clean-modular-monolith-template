using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthzMapVersionConfiguration : IEntityTypeConfiguration<AuthzMapVersion>
{
    public void Configure(EntityTypeBuilder<AuthzMapVersion> builder)
    {
        builder.ToTable("authz_map_version");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Version).IsRequired();
    }
}
