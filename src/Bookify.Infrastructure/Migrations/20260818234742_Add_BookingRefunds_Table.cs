using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_BookingRefunds_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    initiated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_refunds", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_refunds_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_refunds_booking_id",
                table: "booking_refunds",
                column: "booking_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_refunds");
        }
    }
}
