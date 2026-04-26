using System.Security.Claims;
using AuraCore.API.Controllers;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Phase614;

public class FcmDeviceTokenControllerTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"fcm-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    private static MeController BuildController(AuraCoreDbContext db, Guid callerId)
    {
        var claims = new[] { new Claim("sub", callerId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return new MeController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    [Fact]
    public async Task RegisterFcmToken_inserts_new_row()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        var ctrl = BuildController(db, userId);
        var dto = new FcmTokenDto { Token = "ExpoPushToken[abcd1234]", Platform = "android", DeviceId = "device-1" };

        var result = await ctrl.RegisterFcmToken(dto, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        var row = await db.FcmDeviceTokens.FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.Equal(userId, row!.UserId);
        Assert.Equal(dto.Token, row.Token);
        Assert.Equal("android", row.Platform);
    }

    [Fact]
    public async Task RegisterFcmToken_dedups_on_same_user_token_pair()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        db.FcmDeviceTokens.Add(new FcmDeviceToken
        {
            UserId = userId, Token = "DUP", Platform = "android",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var ctrl = BuildController(db, userId);

        var result = await ctrl.RegisterFcmToken(
            new FcmTokenDto { Token = "DUP", Platform = "android" },
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, await db.FcmDeviceTokens.CountAsync());
        var row = await db.FcmDeviceTokens.FirstAsync();
        Assert.True(row.LastSeenAt > row.CreatedAt, "LastSeenAt must update on dedup");
    }

    [Fact]
    public async Task UnregisterFcmToken_removes_row()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        db.FcmDeviceTokens.Add(new FcmDeviceToken { UserId = userId, Token = "T1", Platform = "android" });
        await db.SaveChangesAsync();
        var ctrl = BuildController(db, userId);

        var result = await ctrl.UnregisterFcmToken(new FcmTokenDto { Token = "T1" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.FcmDeviceTokens.CountAsync());
    }

    [Fact]
    public async Task UnregisterFcmToken_returns_NoContent_when_token_not_owned()
    {
        var db = BuildDb();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        db.FcmDeviceTokens.Add(new FcmDeviceToken { UserId = userA, Token = "T1", Platform = "android" });
        await db.SaveChangesAsync();
        var ctrl = BuildController(db, userB);  // different user

        var result = await ctrl.UnregisterFcmToken(new FcmTokenDto { Token = "T1" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, await db.FcmDeviceTokens.CountAsync());  // userA's row untouched
    }
}
