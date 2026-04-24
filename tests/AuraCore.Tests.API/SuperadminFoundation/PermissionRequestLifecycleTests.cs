using System.Net.Http.Json;
using System.Text.Json;
using AuraCore.API.Application.Services.Email;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class PermissionRequestLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _f;

    public PermissionRequestLifecycleTests(WebApplicationFactory<Program> f)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        var dbName = $"prl-{Guid.NewGuid()}";
        var dbRoot = new InMemoryDatabaseRoot();
        _f = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            var dbd = s.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            s.Remove(dbd);
            s.AddDbContext<AuraCoreDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));

            // Override IEmailService with a no-op so controller code doesn't hit
            // the real Resend HTTPS API (would time out or fail in CI).
            var emailDesc = s.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDesc != null) s.Remove(emailDesc);
            s.AddScoped<IEmailService, NullEmailService>();
        }));
    }

    private async Task<(HttpClient client, Guid userId)> AuthedClient(string role)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var u = new User { Id = Guid.NewGuid(), Email = $"{role}-{Guid.NewGuid():N}@x.com", PasswordHash = "x", Role = role, TotpEnabled = true };
        db.Users.Add(u);
        await db.SaveChangesAsync();

        var auth = scope.ServiceProvider.GetRequiredService<AuraCore.API.Application.Interfaces.IAuthService>();
        var token = auth.GenerateAccessToken(u);
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (c, u.Id);
    }

    [Fact]
    public async Task Admin_creates_request_superadmin_approves_grant_exists()
    {
        var (adminC, adminId) = await AuthedClient("admin");
        var (superC, _) = await AuthedClient("superadmin");

        // Step 1: admin creates request
        var createRes = await adminC.PostAsJsonAsync("/api/admin/permission-requests", new {
            permissionKey = "tab:configuration",
            reason = "I need to update SMTP settings for a customer escalation right away",
        });
        createRes.EnsureSuccessStatusCode();
        var createBody = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var requestId = createBody.RootElement.GetProperty("id").GetString()!;

        // Step 2: superadmin lists pending
        var listRes = await superC.GetAsync("/api/superadmin/permission-requests?status=pending");
        listRes.EnsureSuccessStatusCode();
        Assert.Contains("tab:configuration", await listRes.Content.ReadAsStringAsync());

        // Step 3: superadmin approves
        var approveRes = await superC.PostAsJsonAsync($"/api/superadmin/permission-requests/{requestId}/approve",
            new { expiresAt = (string?)null, reviewNote = "Approved; please log steps in audit" });
        approveRes.EnsureSuccessStatusCode();

        // Step 4: grant exists
        using var vs = _f.Services.CreateScope();
        var db = vs.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var grant = await db.PermissionGrants.FirstOrDefaultAsync(g =>
            g.AdminUserId == adminId && g.PermissionKey == "tab:configuration" && g.RevokedAt == null);
        Assert.NotNull(grant);
    }

    [Fact]
    public async Task Admin_cannot_create_duplicate_pending_request()
    {
        var (adminC, _) = await AuthedClient("admin");
        var body = new { permissionKey = "tab:updates", reason = "need to publish a new release urgently ASAP for critical customer issue" };
        var first = await adminC.PostAsJsonAsync("/api/admin/permission-requests", body);
        first.EnsureSuccessStatusCode();

        var dup = await adminC.PostAsJsonAsync("/api/admin/permission-requests", body);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Admin_lists_own_grants_via_my_permissions()
    {
        var (adminC, adminId) = await AuthedClient("admin");

        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
            db.PermissionGrants.Add(new PermissionGrant {
                AdminUserId = adminId, PermissionKey = "tab:updates", GrantedBy = adminId,
            });
            await db.SaveChangesAsync();
        }

        var res = await adminC.GetAsync("/api/admin/my-permissions");
        res.EnsureSuccessStatusCode();
        Assert.Contains("tab:updates", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Superadmin_denies_request_updates_status_and_emits_note()
    {
        var (adminC, _) = await AuthedClient("admin");
        var (superC, _) = await AuthedClient("superadmin");

        var create = await adminC.PostAsJsonAsync("/api/admin/permission-requests", new {
            permissionKey = "action:users.delete", reason = "need to clean a test user account left over from QA",
        });
        create.EnsureSuccessStatusCode();
        var reqId = JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString()!;

        var deny = await superC.PostAsJsonAsync($"/api/superadmin/permission-requests/{reqId}/deny",
            new { reviewNote = "Use the Suspend flow instead" });
        deny.EnsureSuccessStatusCode();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var req = await db.PermissionRequests.FirstAsync(r => r.Id == Guid.Parse(reqId));
        Assert.Equal("denied", req.Status);
        Assert.Equal("Use the Suspend flow instead", req.ReviewNote);
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(string to, string subject, string html, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));

        public Task<EmailSendResult> SendFromTemplateAsync(EmailTemplate template, object data, CancellationToken ct = default)
            => Task.FromResult(new EmailSendResult(true, "test-null", null));
    }
}
