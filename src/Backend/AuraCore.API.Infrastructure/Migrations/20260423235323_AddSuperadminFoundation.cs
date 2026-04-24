using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AuraCore.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperadminFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedVia",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "signup");

            migrationBuilder.AddColumn<bool>(
                name: "ForcePasswordChange",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForcePasswordChangeBy",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReadonly",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordChangedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Require2fa",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "admin_invitations",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_invitations", x => x.TokenHash);
                    table.ForeignKey(
                        name: "FK_admin_invitations_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_invitations_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permission_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_requests_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_permission_requests_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "revoked_tokens",
                columns: table => new
                {
                    Jti = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revoked_tokens", x => x.Jti);
                    table.ForeignKey(
                        name: "FK_revoked_tokens_users_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_revoked_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Key);
                    table.ForeignKey(
                        name: "FK_system_settings_users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "permission_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceRequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_grants_permission_requests_SourceRequestId",
                        column: x => x.SourceRequestId,
                        principalTable: "permission_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_permission_grants_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_permission_grants_users_GrantedBy",
                        column: x => x.GrantedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permission_grants_users_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "app_configs",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTimeOffset(new DateTime(2026, 4, 23, 23, 53, 22, 635, DateTimeKind.Unspecified).AddTicks(1819), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "rate_limit_policies", new DateTimeOffset(new DateTime(2026, 4, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800},\"auth.register\":{\"requests\":3,\"windowSeconds\":3600},\"admin.all\":{\"requests\":1000,\"windowSeconds\":3600},\"signalr.connect\":{\"requests\":10,\"windowSeconds\":60}}" },
                    { "require_2fa_for_all_admins", new DateTimeOffset(new DateTime(2026, 4, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "false" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_CreatedByUserId",
                table: "users",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_invitations_CreatedBy",
                table: "admin_invitations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "ix_admin_invitations_user",
                table: "admin_invitations",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "ix_permission_grants_admin",
                table: "permission_grants",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_grants_GrantedBy",
                table: "permission_grants",
                column: "GrantedBy");

            migrationBuilder.CreateIndex(
                name: "IX_permission_grants_RevokedBy",
                table: "permission_grants",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_permission_grants_SourceRequestId",
                table: "permission_grants",
                column: "SourceRequestId");

            migrationBuilder.CreateIndex(
                name: "uq_permission_grants_active",
                table: "permission_grants",
                columns: new[] { "AdminUserId", "PermissionKey" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_permission_requests_ReviewedBy",
                table: "permission_requests",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "ix_permission_requests_status_admin",
                table: "permission_requests",
                columns: new[] { "Status", "AdminUserId" });

            migrationBuilder.CreateIndex(
                name: "uq_permission_requests_pending",
                table: "permission_requests",
                columns: new[] { "AdminUserId", "PermissionKey" },
                unique: true,
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_revoked_tokens_RevokedBy",
                table: "revoked_tokens",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "ix_revoked_tokens_user",
                table: "revoked_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_UpdatedBy",
                table: "system_settings",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_CreatedByUserId",
                table: "users",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_users_CreatedByUserId",
                table: "users");

            migrationBuilder.DropTable(
                name: "admin_invitations");

            migrationBuilder.DropTable(
                name: "permission_grants");

            migrationBuilder.DropTable(
                name: "revoked_tokens");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "permission_requests");

            migrationBuilder.DropIndex(
                name: "IX_users_CreatedByUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CreatedVia",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ForcePasswordChange",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ForcePasswordChangeBy",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsReadonly",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Require2fa",
                table: "users");

            migrationBuilder.UpdateData(
                table: "app_configs",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTimeOffset(new DateTime(2026, 4, 22, 3, 56, 27, 420, DateTimeKind.Unspecified).AddTicks(7339), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
