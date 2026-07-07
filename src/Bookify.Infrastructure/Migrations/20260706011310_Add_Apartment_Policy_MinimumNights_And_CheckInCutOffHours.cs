using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Apartment_Policy_MinimumNights_And_CheckInCutOffHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cancellation_policy_id",
                table: "apartments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "check_in_cut_off_hours",
                table: "apartments",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "minimum_nights",
                table: "apartments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_apartments_cancellation_policy_id",
                table: "apartments",
                column: "cancellation_policy_id");

            migrationBuilder.AddForeignKey(
                name: "fk_apartments_cancellation_policy_cancellation_policy_id",
                table: "apartments",
                column: "cancellation_policy_id",
                principalTable: "cancellation_policies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_apartments_cancellation_policy_cancellation_policy_id",
                table: "apartments");

            migrationBuilder.DropIndex(
                name: "ix_apartments_cancellation_policy_id",
                table: "apartments");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_id",
                table: "apartments");

            migrationBuilder.DropColumn(
                name: "check_in_cut_off_hours",
                table: "apartments");

            migrationBuilder.DropColumn(
                name: "minimum_nights",
                table: "apartments");
        }
    }
}
