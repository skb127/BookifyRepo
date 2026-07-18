using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Apartment_Capacity_And_ExtraGuestFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "base_guests",
                table: "apartments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "extra_guest_fee_amount",
                table: "apartments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "extra_guest_fee_currency",
                table: "apartments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "max_guests",
                table: "apartments",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "base_guests",
                table: "apartments");

            migrationBuilder.DropColumn(
                name: "extra_guest_fee_amount",
                table: "apartments");

            migrationBuilder.DropColumn(
                name: "extra_guest_fee_currency",
                table: "apartments");

            migrationBuilder.DropColumn(
                name: "max_guests",
                table: "apartments");
        }
    }
}
