using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Apartment_OwnerId_And_Bookings_Write_Permission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "apartments",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "name" },
                values: new object[] { 4, "bookings:write" });

            migrationBuilder.InsertData(
                table: "role_permission",
                columns: new[] { "permission_id", "role_id" },
                values: new object[] { 4, 2 });

            migrationBuilder.CreateIndex(
                name: "ix_apartments_owner_id",
                table: "apartments",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_apartments_user_owner_id",
                table: "apartments",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_apartments_user_owner_id",
                table: "apartments");

            migrationBuilder.DropIndex(
                name: "ix_apartments_owner_id",
                table: "apartments");

            migrationBuilder.DeleteData(
                table: "role_permission",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 4, 2 });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "apartments");
        }
    }
}
