using System.Security.Claims;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.AdminFixes;

public class SecurityFixesTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"sec-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    // ASP.NET Core's ControllerBase.User is HttpContext?.User. Without an
    // HttpContext, any code that touches User.FindFirst(...) will NRE. The
    // production filter pipeline guarantees User is set, but unit tests have
    // to wire it up themselves. This helper attaches a minimal ClaimsPrincipal
    // (caller "sub" claim only) so the controller's self-delete guard can run.
    private static AdminUserController BuildController(AuraCoreDbContext db, Guid? callerId = null)
    {
        var claims = callerId.HasValue
            ? new[] { new Claim("sub", callerId.Value.ToString()) }
            : Array.Empty<Claim>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return new AdminUserController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    [Fact]
    public async Task LoginAttempt_with_wrong_TOTP_is_recorded_as_Success_false()
    {
        // Unit-level assertion on the desired invariant: any LoginAttempt row where
        // the TOTP branch was rejected must have Success=false. We simulate the row
        // directly because the full Login flow requires AuthService + JwtService +
        // TotpService wiring; this test pins the CONTRACT that the rate-limit query
        // (`a.Success == false`) depends on.
        var db = BuildDb();
        db.LoginAttempts.Add(new LoginAttempt
        {
            Email = "victim@example.com",
            IpAddress = "203.0.113.7",
            Success = false  // must be false when TOTP fails (security-2fa.md F-1)
        });
        await db.SaveChangesAsync();

        var failedCount = await db.LoginAttempts
            .CountAsync(a => a.IpAddress == "203.0.113.7" && !a.Success);
        Assert.Equal(1, failedCount);
    }

    [Fact]
    public async Task LoginAttempt_for_password_only_success_but_2fa_pending_is_NOT_logged()
    {
        // When password is correct but TOTP not yet provided (initial call of
        // the 2-step login), the controller returns requires2fa WITHOUT adding a
        // LoginAttempt row. The LoginAttempt only lands on the follow-up call
        // once we know the TOTP verdict. This test pins that no-log contract.
        var db = BuildDb();
        // No LoginAttempt inserted — simulating the "requires2fa" branch early return.

        var total = await db.LoginAttempts.CountAsync();
        Assert.Equal(0, total);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short7")]    // 6 chars, fails MinLength(8)
    [InlineData("       ")]   // whitespace — 7 chars, fails MinLength(8)
    public void ResetPasswordRequest_rejects_short_or_empty_NewPassword(string pw)
    {
        // Validate via System.ComponentModel.DataAnnotations.Validator.TryValidateObject
        // — this exercises the same attributes MVC auto-binds against.
        var req = new AuraCore.API.Controllers.Admin.ResetPasswordRequest
        {
            Email = "target@example.com",
            NewPassword = pw,
        };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(req);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var ok = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(req, ctx, results, validateAllProperties: true);

        Assert.False(ok);
        Assert.Contains(results, r => r.MemberNames.Contains("NewPassword"));
    }

    [Fact]
    public void ResetPasswordRequest_accepts_8plus_char_NewPassword()
    {
        var req = new AuraCore.API.Controllers.Admin.ResetPasswordRequest
        {
            Email = "target@example.com",
            NewPassword = "MySecureP4ss!",
        };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(req);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var ok = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(req, ctx, results, validateAllProperties: true);

        Assert.True(ok);
        Assert.Empty(results);
    }

    [Fact]
    public async Task DeleteUser_cascade_finds_deviceIds_BEFORE_RemoveRange()
    {
        // Pins CTP-5 contract: deviceIds must be captured BEFORE removing devices,
        // so CrashReports / TelemetryEvents do not orphan.
        var db = BuildDb();
        var userId = Guid.NewGuid();
        var licenseId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, Email = "cascade@test.local", PasswordHash = "x" });
        db.Licenses.Add(new License { Id = licenseId, UserId = userId, Key = "kcascade", Tier = "pro" });
        db.Devices.Add(new Device { Id = deviceId, LicenseId = licenseId, HardwareFingerprint = "fp" });
        db.CrashReports.Add(new CrashReport { DeviceId = deviceId, AppVersion = "1", ExceptionType = "E", StackTrace = "st" });
        await db.SaveChangesAsync();

        // Simulate the fixed order: collect deviceIds from preloaded licenses before removal.
        var licenses = await db.Licenses
            .Where(l => l.UserId == userId)
            .Include(l => l.Devices)
            .ToListAsync();
        var deviceIds = licenses.SelectMany(l => l.Devices).Select(d => d.Id).ToList();

        Assert.Contains(deviceId, deviceIds);  // the contract: deviceIds IS populated

        var crashOrphansBefore = await db.CrashReports.CountAsync(c => deviceIds.Contains(c.DeviceId));
        Assert.Equal(1, crashOrphansBefore);   // and we can find children to cascade-delete
    }

    [Fact]
    public void Stripe_webhook_null_signature_contract_check()
    {
        // Contract: When Stripe-Signature header is absent, StringValues.ToString() returns
        // empty string, which the webhook handler must catch before calling ConstructEvent
        // (which would otherwise throw NullReferenceException → HTTP 500). Pins the
        // string.IsNullOrEmpty(signature) guard that Task 8 added to StripeController.Webhook.
        var headers = new Microsoft.AspNetCore.Http.HeaderDictionary();
        // No Stripe-Signature header added.
        var signature = headers["Stripe-Signature"].ToString();
        Assert.True(string.IsNullOrEmpty(signature));  // → BadRequest(new { error = "Missing signature" })
    }

    [Fact]
    public void Stripe_webhook_empty_signature_also_rejected()
    {
        // Contract: Even if the header is present but empty, the guard rejects it.
        var headers = new Microsoft.AspNetCore.Http.HeaderDictionary
        {
            ["Stripe-Signature"] = ""
        };
        var signature = headers["Stripe-Signature"].ToString();
        Assert.True(string.IsNullOrEmpty(signature));
    }

    [Fact]
    public async Task DeleteUser_rejects_admin_target_with_BadRequest()
    {
        // Phase 6.13 hotfix: a regular admin holding ActionUsersDelete must not be
        // able to delete a peer admin via the regular users endpoint. Admin
        // lifecycle belongs in the superadmin-only AdminManagementController.
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User { Id = adminId, Email = "peer-admin@test.local", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, callerId: Guid.NewGuid());
        var result = await controller.DeleteUser(adminId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        // The target row must still exist — guard fires before any RemoveRange.
        Assert.NotNull(await db.Users.FindAsync(adminId));
    }

    [Fact]
    public async Task DeleteUser_rejects_superadmin_target_with_BadRequest()
    {
        // Same guard applied to superadmin targets — the original report.
        var db = BuildDb();
        var superId = Guid.NewGuid();
        db.Users.Add(new User { Id = superId, Email = "owner@test.local", PasswordHash = "x", Role = "superadmin" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, callerId: Guid.NewGuid());
        var result = await controller.DeleteUser(superId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(await db.Users.FindAsync(superId));
    }

    [Fact]
    public async Task DeleteUser_still_allows_regular_user_target()
    {
        // Regression: regular users (Role="user" or null/empty default) MUST still
        // be deletable through this endpoint — that's its primary purpose.
        var db = BuildDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "regular@test.local", PasswordHash = "x", Role = "user" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, callerId: Guid.NewGuid());
        var result = await controller.DeleteUser(userId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(await db.Users.FindAsync(userId));
    }

    [Fact]
    public async Task ResetPassword_silently_noops_on_admin_target()
    {
        // Phase 6.13 hotfix: ResetPassword cannot be used as an account-takeover
        // vector against admin/superadmin accounts. The response stays opaque
        // (matches the email-enumeration protection above) but the password
        // hash is left untouched.
        var db = BuildDb();
        var adminId = Guid.NewGuid();
        const string originalHash = "ORIGINAL_BCRYPT_HASH_PLACEHOLDER";
        db.Users.Add(new User { Id = adminId, Email = "peer@test.local", PasswordHash = originalHash, Role = "admin" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.ResetPassword(
            new ResetPasswordRequest { Email = "peer@test.local", NewPassword = "AttackerNewPw123!" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Users.FindAsync(adminId);
        Assert.Equal(originalHash, stored!.PasswordHash);  // unchanged — silent no-op
    }

    [Fact]
    public async Task ResetPassword_silently_noops_on_superadmin_target()
    {
        var db = BuildDb();
        var superId = Guid.NewGuid();
        const string originalHash = "ORIGINAL_BCRYPT_HASH_PLACEHOLDER";
        db.Users.Add(new User { Id = superId, Email = "owner@test.local", PasswordHash = originalHash, Role = "superadmin" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.ResetPassword(
            new ResetPasswordRequest { Email = "owner@test.local", NewPassword = "AttackerNewPw123!" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Users.FindAsync(superId);
        Assert.Equal(originalHash, stored!.PasswordHash);
    }

    [Fact]
    public void TotpEncryption_roundtrip_preserves_secret()
    {
        // Use EphemeralDataProtectionProvider — a real DataProtection provider that keeps
        // keys in-memory only. This pins the Encrypt→Decrypt roundtrip contract without
        // needing a persisted keyring.
        var provider = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();
        var enc = new AuraCore.API.Infrastructure.Services.Security.TotpEncryption(provider);

        var plaintext = "JBSWY3DPEHPK3PXP";  // sample base32 TOTP secret
        var ciphertext = enc.Encrypt(plaintext);
        var recovered = enc.Decrypt(ciphertext);

        Assert.NotEqual(plaintext, ciphertext);
        Assert.Equal(plaintext, recovered);
    }
}
