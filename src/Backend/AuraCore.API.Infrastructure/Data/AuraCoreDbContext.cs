using AuraCore.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Infrastructure.Data;

public sealed class AuraCoreDbContext : DbContext
{
    public AuraCoreDbContext(DbContextOptions<AuraCoreDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
    public DbSet<CrashReport> CrashReports => Set<CrashReport>();
    public DbSet<AppUpdate> AppUpdates => Set<AppUpdate>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<IpWhitelist> IpWhitelists => Set<IpWhitelist>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    // Phase 6.11 additions
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<AdminInvitation> AdminInvitations => Set<AdminInvitation>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Phase 6.14 additions
    public DbSet<FcmDeviceToken> FcmDeviceTokens => Set<FcmDeviceToken>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>(e => {
            e.ToTable("users"); e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("user");
            e.Property(u => u.TotpSecret).HasMaxLength(64);
            e.Property(u => u.TotpEnabled).HasDefaultValue(false);
            e.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            e.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.Property(u => u.IsReadonly).HasDefaultValue(false);
            e.Property(u => u.ForcePasswordChange).HasDefaultValue(false);
            e.Property(u => u.CreatedVia).HasMaxLength(30).HasDefaultValue("signup");
            e.Property(u => u.Require2fa).HasDefaultValue(false);
            e.HasOne<User>().WithMany().HasForeignKey(u => u.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<License>(e => {
            e.ToTable("licenses"); e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(l => l.Key).HasMaxLength(128).IsRequired();
            e.HasIndex(l => l.Key).IsUnique();
            e.Property(l => l.Tier).HasMaxLength(20).HasDefaultValue("free");
            e.Property(l => l.Status).HasMaxLength(20).HasDefaultValue("active");
            e.Property(l => l.MaxDevices).HasDefaultValue(1);
            e.Property(l => l.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(l => l.User).WithMany(u => u.Licenses).HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Device>(e => {
            e.ToTable("devices"); e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(d => d.HardwareFingerprint).HasMaxLength(512).IsRequired();
            e.Property(d => d.MachineName).HasMaxLength(256);
            e.Property(d => d.OsVersion).HasMaxLength(128);
            e.Property(d => d.RegisteredAt).HasDefaultValueSql("now()");
            e.Property(d => d.LastSeenAt).HasDefaultValueSql("now()");
            e.HasIndex(d => new { d.LicenseId, d.HardwareFingerprint }).IsUnique();
            e.HasOne(d => d.License).WithMany(l => l.Devices).HasForeignKey(d => d.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Subscription>(e => {
            e.ToTable("subscriptions"); e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.StripeSubscriptionId).HasMaxLength(256);
            e.Property(s => s.StripeCustomerId).HasMaxLength(256);
            e.Property(s => s.Plan).HasMaxLength(20).HasDefaultValue("monthly");
            e.Property(s => s.Status).HasMaxLength(20).HasDefaultValue("active");
            e.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(s => s.User).WithMany(u => u.Subscriptions).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<TelemetryEvent>(e => {
            e.ToTable("telemetry_events"); e.HasKey(t => t.Id);
            e.Property(t => t.Id).UseIdentityAlwaysColumn();
            e.Property(t => t.EventType).HasMaxLength(128).IsRequired();
            e.Property(t => t.EventData).HasColumnType("jsonb").HasDefaultValue("{}");
            e.Property(t => t.SessionId).HasMaxLength(64);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(t => t.CreatedAt); e.HasIndex(t => t.EventType);
            e.HasOne(t => t.Device).WithMany(d => d.TelemetryEvents).HasForeignKey(t => t.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<CrashReport>(e => {
            e.ToTable("crash_reports"); e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.AppVersion).HasMaxLength(32).IsRequired();
            e.Property(c => c.ExceptionType).HasMaxLength(512).IsRequired();
            e.Property(c => c.StackTrace).IsRequired();
            e.Property(c => c.SystemInfo).HasColumnType("jsonb").HasDefaultValue("{}");
            e.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(c => c.CreatedAt);
            e.HasOne(c => c.Device).WithMany(d => d.CrashReports).HasForeignKey(c => c.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<AppUpdate>(e => {
            e.ToTable("app_updates"); e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Version).HasMaxLength(32).IsRequired();
            e.Property(u => u.Channel).HasMaxLength(20).HasDefaultValue("stable");
            e.Property(u => u.Platform).HasDefaultValue(AppUpdatePlatform.Windows);
            e.HasIndex(u => new { u.Version, u.Channel, u.Platform }).IsUnique();
            e.Property(u => u.BinaryUrl).HasMaxLength(1024);
            e.Property(u => u.SignatureHash).HasMaxLength(256);
            e.Property(u => u.GitHubReleaseId).HasMaxLength(64);
            e.Property(u => u.PublishedAt).HasDefaultValueSql("now()");
        });

        m.Entity<RefreshToken>(e => {
            e.ToTable("refresh_tokens"); e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.Token).HasMaxLength(512).IsRequired();
            e.HasIndex(r => r.Token).IsUnique();
            e.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Payment>(e => {
            e.ToTable("payments"); e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.Provider).HasMaxLength(20).IsRequired();
            e.Property(p => p.ExternalId).HasMaxLength(512);
            e.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.Amount).HasColumnType("decimal(10,2)");
            e.Property(p => p.Currency).HasMaxLength(10).HasDefaultValue("USD");
            e.Property(p => p.Plan).HasMaxLength(20);
            e.Property(p => p.Tier).HasMaxLength(20);
            e.Property(p => p.CryptoAddress).HasMaxLength(256);
            e.Property(p => p.CryptoTxHash).HasMaxLength(256);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(p => p.ExternalId);
            e.HasIndex(p => p.CreatedAt);
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<LoginAttempt>(e => {
            e.ToTable("login_attempts"); e.HasKey(a => a.Id);
            e.Property(a => a.Id).UseIdentityAlwaysColumn();
            e.Property(a => a.Email).HasMaxLength(256).IsRequired();
            e.Property(a => a.IpAddress).HasMaxLength(45).IsRequired();
            e.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(a => new { a.Email, a.CreatedAt });
            e.HasIndex(a => new { a.IpAddress, a.CreatedAt });
        });

        m.Entity<IpWhitelist>(e => {
            e.ToTable("ip_whitelists"); e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(i => i.IpAddress).HasMaxLength(45).IsRequired();
            e.HasIndex(i => i.IpAddress).IsUnique();
            e.Property(i => i.Label).HasMaxLength(256);
            e.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
        });

        m.Entity<PasswordResetCode>(e => {
            e.ToTable("password_reset_codes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.Code).HasMaxLength(6).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.Email);
        });

        m.Entity<AppConfig>(e => {
            e.ToTable("app_configs"); e.HasKey(c => c.Id);
            e.Property(c => c.MaintenanceMessage).HasDefaultValue("");
            e.Property(c => c.IsMaintenanceMode).HasDefaultValue(false);
            e.Property(c => c.NewRegistrations).HasDefaultValue(true);
            e.Property(c => c.TelemetryEnabled).HasDefaultValue(true);
            e.Property(c => c.CrashReportsEnabled).HasDefaultValue(true);
            e.Property(c => c.AutoUpdateEnabled).HasDefaultValue(true);
            e.Property(c => c.LastUpdated).HasDefaultValueSql("now()");
            e.HasData(new AppConfig { Id = 1 });
        });

        m.Entity<AuditLogEntry>(e => {
            e.ToTable("audit_log");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).UseIdentityAlwaysColumn();
            e.Property(a => a.ActorEmail).HasMaxLength(256).IsRequired();
            e.Property(a => a.Action).HasMaxLength(64).IsRequired();
            e.Property(a => a.TargetType).HasMaxLength(32).IsRequired();
            e.Property(a => a.TargetId).HasMaxLength(128);
            e.Property(a => a.BeforeData).HasColumnType("jsonb");
            e.Property(a => a.AfterData).HasColumnType("jsonb");
            e.Property(a => a.IpAddress).HasMaxLength(45);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(a => new { a.ActorId, a.CreatedAt }).HasDatabaseName("idx_audit_actor_created");
            e.HasIndex(a => new { a.Action, a.CreatedAt }).HasDatabaseName("idx_audit_action_created");
            e.HasIndex(a => new { a.TargetType, a.TargetId }).HasDatabaseName("idx_audit_target");
            e.HasIndex(a => a.CreatedAt).HasDatabaseName("idx_audit_created");
            e.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<PermissionGrant>(e => {
            e.ToTable("permission_grants"); e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.PermissionKey).HasMaxLength(100).IsRequired();
            e.Property(p => p.GrantedAt).HasDefaultValueSql("now()");
            e.Property(p => p.RevokeReason).HasMaxLength(500);
            e.HasOne(p => p.AdminUser).WithMany().HasForeignKey(p => p.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.GrantedByUser).WithMany().HasForeignKey(p => p.GrantedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.RevokedByUser).WithMany().HasForeignKey(p => p.RevokedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.SourceRequest).WithMany().HasForeignKey(p => p.SourceRequestId).OnDelete(DeleteBehavior.SetNull);
            // Partial unique: only one ACTIVE (not revoked) grant per (admin, key).
            e.HasIndex(p => new { p.AdminUserId, p.PermissionKey })
             .HasFilter("\"RevokedAt\" IS NULL")
             .IsUnique()
             .HasDatabaseName("uq_permission_grants_active");
            e.HasIndex(p => p.AdminUserId).HasDatabaseName("ix_permission_grants_admin");
        });

        m.Entity<PermissionRequest>(e => {
            e.ToTable("permission_requests"); e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.PermissionKey).HasMaxLength(100).IsRequired();
            e.Property(p => p.Reason).IsRequired();
            e.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.RequestedAt).HasDefaultValueSql("now()");
            e.Property(p => p.ReviewNote).HasMaxLength(1000);
            e.HasOne(p => p.AdminUser).WithMany().HasForeignKey(p => p.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.ReviewedByUser).WithMany().HasForeignKey(p => p.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(p => new { p.Status, p.AdminUserId }).HasDatabaseName("ix_permission_requests_status_admin");
            // Partial unique: only one PENDING request per (admin, key).
            e.HasIndex(p => new { p.AdminUserId, p.PermissionKey })
             .HasFilter("\"Status\" = 'pending'")
             .IsUnique()
             .HasDatabaseName("uq_permission_requests_pending");
        });

        m.Entity<RevokedToken>(e => {
            e.ToTable("revoked_tokens"); e.HasKey(r => r.Jti);
            e.Property(r => r.Jti).HasMaxLength(100).IsRequired();
            e.Property(r => r.RevokedAt).HasDefaultValueSql("now()");
            e.Property(r => r.RevokeReason).HasMaxLength(100).IsRequired();
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.RevokedByUser).WithMany().HasForeignKey(r => r.RevokedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => r.UserId).HasDatabaseName("ix_revoked_tokens_user");
        });

        m.Entity<AdminInvitation>(e => {
            e.ToTable("admin_invitations"); e.HasKey(i => i.TokenHash);
            e.Property(i => i.TokenHash).HasMaxLength(100).IsRequired();
            e.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(i => i.AdminUser).WithMany().HasForeignKey(i => i.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.CreatedByUser).WithMany().HasForeignKey(i => i.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => i.AdminUserId).HasDatabaseName("ix_admin_invitations_user");
        });

        m.Entity<SystemSetting>(e => {
            e.ToTable("system_settings"); e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
            e.Property(s => s.Value).IsRequired();
            e.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(s => s.UpdatedByUser).WithMany().HasForeignKey(s => s.UpdatedBy).OnDelete(DeleteBehavior.SetNull);

            e.HasData(
                new SystemSetting { Key = "require_2fa_for_all_admins", Value = "false", UpdatedAt = new DateTimeOffset(2026, 4, 23, 0, 0, 0, TimeSpan.Zero) },
                new SystemSetting { Key = "rate_limit_policies",
                    Value = "{\"auth.login\":{\"requests\":5,\"windowSeconds\":1800},\"auth.register\":{\"requests\":3,\"windowSeconds\":3600},\"admin.all\":{\"requests\":1000,\"windowSeconds\":3600},\"signalr.connect\":{\"requests\":10,\"windowSeconds\":60}}",
                    UpdatedAt = new DateTimeOffset(2026, 4, 23, 0, 0, 0, TimeSpan.Zero) },
                new SystemSetting { Key = "audit_retention.retentionDays", Value = "365", UpdatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero) },
                new SystemSetting { Key = "audit_retention.lastRunAt", Value = "", UpdatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero) },
                new SystemSetting { Key = "audit_retention.lastRunDeletedRows", Value = "0", UpdatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero) }
            );
        });

        m.Entity<FcmDeviceToken>(b =>
        {
            b.ToTable("fcm_device_tokens");
            b.HasKey(t => t.Id);
            b.Property(t => t.Token).HasMaxLength(512).IsRequired();
            b.Property(t => t.Platform).HasMaxLength(16).IsRequired();
            b.Property(t => t.DeviceId).HasMaxLength(256);
            b.HasIndex(t => t.UserId);
            b.HasIndex(t => new { t.UserId, t.Token }).IsUnique();
        });
    }
}
