using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_authz_role_permissions_PermissionId",
                table: "authz_role_permissions",
                column: "PermissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_authz_role_permissions_authz_permissions_PermissionId",
                table: "authz_role_permissions",
                column: "PermissionId",
                principalTable: "authz_permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_authz_role_permissions_authz_roles_RoleId",
                table: "authz_role_permissions",
                column: "RoleId",
                principalTable: "authz_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_authz_role_permissions_authz_permissions_PermissionId",
                table: "authz_role_permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_authz_role_permissions_authz_roles_RoleId",
                table: "authz_role_permissions");

            migrationBuilder.DropIndex(
                name: "IX_authz_role_permissions_PermissionId",
                table: "authz_role_permissions");
        }
    }
}
