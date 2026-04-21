using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCore.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformToAppUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_app_updates_Version_Channel",
                table: "app_updates");

            migrationBuilder.AddColumn<string>(
                name: "GitHubReleaseId",
                table: "app_updates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Platform",
                table: "app_updates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "app_configs",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTimeOffset(new DateTime(2026, 4, 21, 2, 53, 45, 4, DateTimeKind.Unspecified).AddTicks(2290), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_app_updates_Version_Channel_Platform",
                table: "app_updates",
                columns: new[] { "Version", "Channel", "Platform" },
                unique: true);

            migrationBuilder.Sql(@"
    INSERT INTO app_updates
    (""Id"", ""Version"", ""Channel"", ""Platform"", ""ReleaseNotes"", ""BinaryUrl"",
     ""SignatureHash"", ""IsMandatory"", ""PublishedAt"", ""GitHubReleaseId"")
    SELECT gen_random_uuid(), '1.6.0', 'stable', 1,
           'Legacy Windows release migrated from GitHub.',
           'https://github.com/edutuvarna/AuraCorePro/releases/download/v1.6.0/AuraCorePro-Setup.exe',
           '', false, '2026-01-15T00:00:00Z'::timestamptz, NULL
    WHERE NOT EXISTS (
        SELECT 1 FROM app_updates WHERE ""Version"" = '1.6.0' AND ""Platform"" = 1
    );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_app_updates_Version_Channel_Platform",
                table: "app_updates");

            migrationBuilder.Sql(@"DELETE FROM app_updates WHERE ""Version"" = '1.6.0' AND ""BinaryUrl"" LIKE '%github.com%'.");

            migrationBuilder.DropColumn(
                name: "GitHubReleaseId",
                table: "app_updates");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "app_updates");

            migrationBuilder.UpdateData(
                table: "app_configs",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTimeOffset(new DateTime(2026, 4, 21, 2, 53, 5, 240, DateTimeKind.Unspecified).AddTicks(9263), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_app_updates_Version_Channel",
                table: "app_updates",
                columns: new[] { "Version", "Channel" },
                unique: true);
        }
    }
}
