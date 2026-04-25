using AuraCore.API.Application.Services.Push;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Phase614;

public class PermissionRequestPushTriggerTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"push-trig-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    private sealed class FakeFcm : IFcmService
    {
        public List<(string token, FcmPayload payload)> Sent { get; } = new();
        public Task SendAsync(string deviceToken, FcmPayload payload, CancellationToken ct)
        {
            Sent.Add((deviceToken, payload));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Push_trigger_enumerates_superadmin_FCM_tokens_and_sends_one_per_token()
    {
        var db = BuildDb();
        var super1 = Guid.NewGuid();
        var super2 = Guid.NewGuid();
        var regularAdmin = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = super1, Email = "s1@test", PasswordHash = "x", Role = "superadmin" },
            new User { Id = super2, Email = "s2@test", PasswordHash = "x", Role = "superadmin" },
            new User { Id = regularAdmin, Email = "a@test", PasswordHash = "x", Role = "admin" }
        );
        db.FcmDeviceTokens.AddRange(
            new FcmDeviceToken { UserId = super1, Token = "T-S1", Platform = "android" },
            new FcmDeviceToken { UserId = super2, Token = "T-S2-A", Platform = "android" },
            new FcmDeviceToken { UserId = super2, Token = "T-S2-B", Platform = "android" },
            new FcmDeviceToken { UserId = regularAdmin, Token = "T-A", Platform = "android" }
        );
        await db.SaveChangesAsync();

        var fcm = new FakeFcm();
        var payload = new FcmPayload("Permission request",
            "admin@example.com requests tab:audit",
            new Dictionary<string, string> { ["type"] = "permission-request", ["requestId"] = Guid.NewGuid().ToString() });

        await AuraCore.API.Controllers.Admin.PermissionRequestPushTrigger
            .SendToSuperadminsAsync(db, fcm, payload, CancellationToken.None);

        Assert.Equal(3, fcm.Sent.Count);
        Assert.Contains(fcm.Sent, x => x.token == "T-S1");
        Assert.Contains(fcm.Sent, x => x.token == "T-S2-A");
        Assert.Contains(fcm.Sent, x => x.token == "T-S2-B");
        Assert.DoesNotContain(fcm.Sent, x => x.token == "T-A"); // regular admin excluded
    }
}
