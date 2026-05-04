using System;
using AuraCore.Application;
using Xunit;

namespace AuraCore.Tests.Unit;

public class OperationResultTests
{
    [Fact]
    public void Success_HasStatusSuccess_AndReportsBytesAndItems()
    {
        var r = OperationResult.Success(2_500_000_000L, 1247, TimeSpan.FromSeconds(4.2));
        Assert.Equal(OperationStatus.Success, r.Status);
        Assert.Equal(2_500_000_000L, r.BytesFreed);
        Assert.Equal(1247, r.ItemsAffected);
        Assert.Null(r.Reason);
        Assert.Null(r.RemediationCommand);
    }

    [Fact]
    public void Skipped_CarriesReason_AndOptionalRemediation()
    {
        var r = OperationResult.Skipped("Privilege helper required", "sudo bash /opt/install.sh");
        Assert.Equal(OperationStatus.Skipped, r.Status);
        Assert.Equal(0L, r.BytesFreed);
        Assert.Equal(0, r.ItemsAffected);
        Assert.Equal("Privilege helper required", r.Reason);
        Assert.Equal("sudo bash /opt/install.sh", r.RemediationCommand);
        Assert.Equal(TimeSpan.Zero, r.Duration);
    }

    [Fact]
    public void Failed_CarriesReasonAndDuration_NoRemediation()
    {
        var r = OperationResult.Failed("drop_caches sysctl returned EACCES", TimeSpan.FromMilliseconds(120));
        Assert.Equal(OperationStatus.Failed, r.Status);
        Assert.Equal("drop_caches sysctl returned EACCES", r.Reason);
        Assert.Null(r.RemediationCommand);
        Assert.Equal(TimeSpan.FromMilliseconds(120), r.Duration);
    }

    [Fact]
    public void Skipped_NoRemediation_NullableArgDefaultsToNull()
    {
        var r = OperationResult.Skipped("Feature flag off");
        Assert.Equal(OperationStatus.Skipped, r.Status);
        Assert.Equal("Feature flag off", r.Reason);
        Assert.Null(r.RemediationCommand);
    }
}
