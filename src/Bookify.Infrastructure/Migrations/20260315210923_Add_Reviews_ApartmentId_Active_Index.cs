using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Reviews_ApartmentId_Active_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reviews_apartment_id",
                table: "reviews");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_apartment_id",
                table: "reviews",
                column: "apartment_id",
                filter: "deleted_on_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reviews_apartment_id",
                table: "reviews");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_apartment_id",
                table: "reviews",
                column: "apartment_id");
        }
    }
}
