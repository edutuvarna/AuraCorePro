using AuraCore.API.Domain.Entities;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class EntityDefaultsTests
{
    [Fact]
    public void PermissionGrant_defaults_are_sensible()
    {
        var g = new PermissionGrant();
        Assert.NotEqual(Guid.Empty, g.Id);
        Assert.Null(g.RevokedAt);
        Assert.Null(g.ExpiresAt);
        Assert.True(g.IsActive());
    }

    [Fact]
    public void PermissionGrant_expired_is_inactive()
    {
        var g = new PermissionGrant { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        Assert.False(g.IsActive());
    }

    [Fact]
    public void PermissionGrant_revoked_is_inactive()
    {
        var g = new PermissionGrant { RevokedAt = DateTimeOffset.UtcNow };
        Assert.False(g.IsActive());
    }

    [Fact]
    public void PermissionRequest_status_defaults_to_pending()
    {
        var r = new PermissionRequest();
        Assert.Equal("pending", r.Status);
    }

    [Fact]
    public void AdminInvitation_valid_when_not_consumed_and_not_expired()
    {
        var inv = new AdminInvitation { ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        Assert.True(inv.IsValid());
    }

    [Fact]
    public void AdminInvitation_invalid_after_consumed()
    {
        var inv = new AdminInvitation {
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            ConsumedAt = DateTimeOffset.UtcNow,
        };
        Assert.False(inv.IsValid());
    }

    [Fact]
    public void User_defaults_for_new_phase611_fields()
    {
        var u = new User();
        Assert.True(u.IsActive);
        Assert.False(u.IsReadonly);
        Assert.False(u.ForcePasswordChange);
        Assert.Null(u.ForcePasswordChangeBy);
        Assert.Null(u.PasswordChangedAt);
        Assert.Null(u.CreatedByUserId);
        Assert.Equal("signup", u.CreatedVia);
        Assert.False(u.Require2fa);
    }
}
