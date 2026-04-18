using System;
using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

public class InstanceMutexTests
{
    private static string UniqueName() => $"AuraCorePro-Test-{Guid.NewGuid():N}";

    [Fact]
    public void TryAcquire_on_fresh_mutex_returns_true()
    {
        using var m = new InstanceMutex(UniqueName());
        Assert.True(m.TryAcquire());
    }

    [Fact]
    public void TryAcquire_twice_on_same_name_returns_true_then_false()
    {
        var name = UniqueName();
        using var first = new InstanceMutex(name);
        using var second = new InstanceMutex(name);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void TryAcquire_after_dispose_allows_new_owner()
    {
        var name = UniqueName();

        using (var first = new InstanceMutex(name))
        {
            Assert.True(first.TryAcquire());
        } // dispose releases mutex

        using var second = new InstanceMutex(name);
        Assert.True(second.TryAcquire());
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var m = new InstanceMutex(UniqueName());
        m.TryAcquire();
        m.Dispose();
        m.Dispose(); // must not throw
    }
}
