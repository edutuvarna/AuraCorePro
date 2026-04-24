using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class DualRoleClaimTests
{
    private static (AuthService svc, AuraCoreDbContext db) BuildSvc()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"jwt-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opt);
        var cfg = new ConfigurationBuilder().Build();
        return (new AuthService(db, cfg), db);
    }

    private static JwtSecurityToken Decode(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Admin_token_has_single_role_claim()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var t = svc.GenerateAccessToken(user);
        var jwt = Decode(t);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Single(roles);
        Assert.Equal("admin", roles[0]);
    }

    [Fact]
    public void Superadmin_token_has_dual_role_claims_admin_and_superadmin()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "sa@b.com", Role = "superadmin" };
        var t = svc.GenerateAccessToken(user);
        var jwt = Decode(t);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Contains("admin", roles);
        Assert.Contains("superadmin", roles);
    }

    [Fact]
    public void Every_token_has_unique_jti()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var t1 = Decode(svc.GenerateAccessToken(user));
        var t2 = Decode(svc.GenerateAccessToken(user));

        var jti1 = t1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = t2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        Assert.NotEqual(jti1, jti2);
    }

    [Fact]
    public void Scope_limited_token_has_scope_claim_and_shorter_expiry()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var token = svc.GenerateAccessToken(user, scope: "2fa-setup-only", lifetime: TimeSpan.FromMinutes(15));
        var jwt = Decode(token);

        var scope = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
        Assert.Equal("2fa-setup-only", scope);

        var remaining = jwt.ValidTo - DateTime.UtcNow;
        Assert.True(remaining <= TimeSpan.FromMinutes(16));
        Assert.True(remaining >= TimeSpan.FromMinutes(13));
    }

    [Fact]
    public void Default_token_has_no_scope_claim()
    {
        var (svc, _) = BuildSvc();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.com", Role = "admin" };
        var token = svc.GenerateAccessToken(user);
        var jwt = Decode(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "scope");
    }
}
