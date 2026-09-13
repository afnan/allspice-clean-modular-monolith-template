using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Migrations
{
    /// <summary>
    /// Snapshot-only resync: no schema change, intentionally empty Up/Down.
    ///
    /// <see cref="Domain.Aggregates.Authorization.AuthzMapVersion" /> maps PostgreSQL's system
    /// <c>xmin</c> column as an optimistic-concurrency token (see
    /// Infrastructure/Persistence/Configurations/AuthzMapVersionConfiguration.cs, added in
    /// 6fa7d70 without a migration, by design: xmin already exists on every Postgres row, it is
    /// not a column EF can create). The design-time model differ does not know that and, left to
    /// its default, generates <c>AddColumn "xmin" ... rowVersion: true</c> / matching
    /// <c>DropColumn</c> for this migration — which would fail at runtime because Postgres
    /// rejects a user column named <c>xmin</c> ("column name conflicts with a system column
    /// name"). Both operations are removed here; the migration exists solely so the model
    /// snapshot (<see cref="IdentityDbContextModelSnapshot" />) picks up the xmin mapping and the
    /// PendingModelChangesWarning EF raises for the unsynced model goes away, per ADR-0002's
    /// "envelope tables are Wolverine's, not EF's" precedent — this is the analogous case for a
    /// property whose backing "column" already exists outside of EF's control.
    /// </summary>
    public partial class SyncAuthzMapVersionXminSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
