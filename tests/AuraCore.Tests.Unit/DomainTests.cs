using Xunit;
using AuraCore.Domain.ValueObjects;
using AuraCore.SharedKernel;
namespace AuraCore.Tests.Unit;

public class DomainTests
{
    [Fact] public void HealthScore_Valid() => Assert.Equal(85, new HealthScore(85).Value);
    [Fact] public void HealthScore_Invalid() => Assert.Throws<ArgumentOutOfRangeException>(() => new HealthScore(101));
    [Fact] public void Result_Success() { var r = Result.Success(); Assert.True(r.IsSuccess); }
    [Fact] public void Result_Failure() { var r = Result.Failure("err"); Assert.False(r.IsSuccess); Assert.Equal("err", r.Error); }
    [Fact] public void Guard_Null() => Assert.Throws<ArgumentNullException>(() => Guard.AgainstNull<string>(null, "p"));
}
