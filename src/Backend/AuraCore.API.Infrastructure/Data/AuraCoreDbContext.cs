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
            e.HasIndex(u => new { u.Version, u.Channel }).IsUnique();
            e.Property(u => u.Channel).HasMaxLength(20).HasDefaultValue("stable");
            e.Property(u => u.BinaryUrl).HasMaxLength(1024);
            e.Property(u => u.SignatureHash).HasMaxLength(256);
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
    }
}
