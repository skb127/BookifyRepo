using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_CancellationPolicy_SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "cancellation_policies",
                keyColumn: "id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "cancellation_policies",
                columns: new[] { "id", "created_on_utc", "early_guest_penalty_rate", "early_host_penalty_rate", "is_default", "late_guest_penalty_rate", "late_host_penalty_rate", "name", "threshold_hours" },
                values: new object[] { new Guid("c0000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 13, 0, 0, 0, 0, DateTimeKind.Utc), 0.10m, 0.10m, true, 0.50m, 0.50m, "Default Cancellation Policy", 48 });
        }
    }
}
