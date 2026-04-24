using System.Net.Http.Json;
using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class AdminActionLogCsvExportTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public AdminActionLogCsvExportTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        // Shared InMemoryDatabaseRoot so seeded data is visible across scopes
        // (the HTTP request's DbContext scope would otherwise see an empty store).
        var dbName = $"cal-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var d = s.Single(x => x.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(d);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));
        }));
    }

    private async Task<HttpClient> Authed(string role)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = new User { Id = Guid.NewGuid(), Email = $"{role}@x.com", PasswordHash = "x", Role = role, TotpEnabled = true };
        db.Users.Add(u); await db.SaveChangesAsync();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.GenerateAccessToken(u));
        return c;
    }

    [Fact]
    public async Task Superadmin_admin_actions_csv_contains_header_and_rows()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var actorId = Guid.NewGuid();
        db.Users.Add(new User { Id = actorId, Email = "admin-actor@x.com", PasswordHash = "x", Role = "admin" });
        db.AuditLogs.Add(new AuditLogEntry { ActorId = actorId, ActorEmail = "admin-actor@x.com", Action = "DeleteUser", TargetType = "User", TargetId = Guid.NewGuid().ToString() });
        db.AuditLogs.Add(new AuditLogEntry { ActorId = null, ActorEmail = "system@x.com", Action = "AutoMigrate", TargetType = "System" });
        await db.SaveChangesAsync();

        var c = await Authed("superadmin");
        var res = await c.GetAsync("/api/superadmin/admin-actions/export.csv");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.StartsWith("\"id\",\"actor_email\",\"actor_id\",\"action\",\"target_type\",\"target_id\",\"ip_address\",\"created_at_utc\"", body);
        Assert.Contains("DeleteUser", body);
        // System-scoped row should NOT appear (role-filtered to admin actors)
        Assert.DoesNotContain("AutoMigrate", body);
    }

    [Fact]
    public async Task Admin_audit_log_export_csv_includes_all_rows()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        db.AuditLogs.Add(new AuditLogEntry { ActorEmail = "anyone@x.com", Action = "X", TargetType = "Y" });
        await db.SaveChangesAsync();

        var c = await Authed("admin");
        var res = await c.GetAsync("/api/admin/audit-log/export.csv");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"action\"", body);
    }
}
