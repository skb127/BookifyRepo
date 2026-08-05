using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Reviews_Write_And_AdminDelete_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 9, "reviews:write" },
                    { 10, "reviews:admin-delete" }
                });

            migrationBuilder.InsertData(
                table: "role_permission",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { 9, 3 },
                    { 9, 4 },
                    { 10, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 9, 3 });

            migrationBuilder.DeleteData(
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 9, 4 });

            migrationBuilder.DeleteData(
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 10, 2 });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: 10);
        }
    }
}
