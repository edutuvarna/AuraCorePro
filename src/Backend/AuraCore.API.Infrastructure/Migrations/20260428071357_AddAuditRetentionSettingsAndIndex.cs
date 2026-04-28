using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AuraCore.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRetentionSettingsAndIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_configs",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTimeOffset(new DateTime(2026, 4, 28, 7, 13, 56, 638, DateTimeKind.Unspecified).AddTicks(7402), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "audit_retention.lastRunAt", new DateTimeOffset(new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "" },
                    { "audit_retention.lastRunDeletedRows", new DateTimeOffset(new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "0" },
                    { "audit_retention.retentionDays", new DateTimeOffset(new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "365" }
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_created",
                table: "audit_log",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_audit_created",
                table: "audit_log");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "Key",
                keyValue: "audit_retention.lastRunAt");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "Key",
                keyValue: "audit_retention.lastRunDeletedRows");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "Key",
                keyValue: "audit_retention.retentionDays");

            migrationBuilder.UpdateData(
                table: "app_configs",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTimeOffset(new DateTime(2026, 4, 25, 16, 22, 17, 254, DateTimeKind.Unspecified).AddTicks(8996), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
