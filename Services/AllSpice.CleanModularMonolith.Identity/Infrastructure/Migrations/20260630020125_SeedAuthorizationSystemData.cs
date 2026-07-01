using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedAuthorizationSystemData : Migration
    {
        // Fixed deterministic timestamp: 2026-06-30T00:00:00Z
        private static readonly DateTimeOffset SeedDate = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "authz_permissions",
                columns: ["Id", "Key", "Description", "IsSystem", "CreatedOnUtc", "CreatedBy", "LastModifiedOnUtc", "LastModifiedBy"],
                values: new object[,]
                {
                    { Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "authz.read",   "Read authorization config",   true, SeedDate, null, null, null },
                    { Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "authz.manage", "Manage authorization config", true, SeedDate, null, null, null },
                });

            migrationBuilder.InsertData(
                table: "authz_map_version",
                columns: ["Id", "Version"],
                values: new object[] { Guid.Parse("00000000-0000-0000-0000-0000000000b1"), 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "authz_map_version",
                keyColumn: "Id",
                keyValue: Guid.Parse("00000000-0000-0000-0000-0000000000b1"));

            migrationBuilder.DeleteData(
                table: "authz_permissions",
                keyColumn: "Id",
                keyValue: Guid.Parse("00000000-0000-0000-0000-0000000000a1"));

            migrationBuilder.DeleteData(
                table: "authz_permissions",
                keyColumn: "Id",
                keyValue: Guid.Parse("00000000-0000-0000-0000-0000000000a2"));
        }
    }
}
