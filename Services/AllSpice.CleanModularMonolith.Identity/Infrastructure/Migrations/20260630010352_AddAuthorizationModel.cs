using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authz_map_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authz_map_version", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authz_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authz_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authz_role_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authz_role_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authz_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authz_roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authz_permissions_Key",
                table: "authz_permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_authz_role_permissions_RoleId",
                table: "authz_role_permissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_authz_role_permissions_RoleId_PermissionId",
                table: "authz_role_permissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_authz_roles_Key",
                table: "authz_roles",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authz_map_version");

            migrationBuilder.DropTable(
                name: "authz_permissions");

            migrationBuilder.DropTable(
                name: "authz_role_permissions");

            migrationBuilder.DropTable(
                name: "authz_roles");
        }
    }
}
