using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

internal sealed class IdentitySyncHistoryConfiguration : IEntityTypeConfiguration<IdentitySyncHistory>
{
    public void Configure(EntityTypeBuilder<IdentitySyncHistory> builder)
    {
        builder.ToTable("IdentitySyncHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.JobName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(history => history.ErrorMessage)
            .HasMaxLength(2048);

        builder.Property(history => history.CorrelationId)
            .HasMaxLength(64);
    }
}


