using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

internal sealed class IdentitySyncIssueConfiguration : IEntityTypeConfiguration<IdentitySyncIssue>
{
    public void Configure(EntityTypeBuilder<IdentitySyncIssue> builder)
    {
        builder.ToTable("IdentitySyncIssues");

        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.IssueType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(issue => issue.Message)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(issue => issue.Details)
            .HasMaxLength(4000);

        builder.HasIndex(issue => new { issue.IssueType, issue.ResolvedUtc });
    }
}


