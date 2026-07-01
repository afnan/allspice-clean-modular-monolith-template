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
        builder.Property(v => v.Version);

        // The single monotonic version row is bumped read-modify-write (see AuthzMapVersion.Bump).
        // Map PostgreSQL's system xmin column as an optimistic-concurrency token so two concurrent
        // authz mutations racing the same row produce a DbUpdateConcurrencyException on the loser
        // instead of silently losing an increment. xmin is a system column that already exists on
        // every row — this needs no entity property, no new column, and no migration. Expressed with
        // core EF fluent API (the equivalent of Npgsql's UseXminAsConcurrencyToken) because this module
        // references Npgsql only transitively via the Aspire component; the "xmin" shadow property is
        // dropped for the SQLite-backed tests (see SqliteJsonbModelCustomizer).
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
