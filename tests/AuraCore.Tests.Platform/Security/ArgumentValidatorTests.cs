using AuraCore.PrivilegedService.Ops;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.Security;

public class ArgumentValidatorTests
{
    [Theory]
    [InlineData("normal-value")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("Defender.Signature.Update")]
    [InlineData("service_name-01")]
    public void IsSafeArgument_returns_true_for_benign_values(string value)
    {
        ArgumentValidator.IsSafeArgument(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("value; rm -rf /")]
    [InlineData("value | ls")]
    [InlineData("value & calc")]
    [InlineData("value`whoami`")]
    [InlineData("value$(id)")]
    [InlineData("value\nnewline")]
    [InlineData("value\rcr")]
    [InlineData("value>out.txt")]
    [InlineData("value<input.txt")]
    public void IsSafeArgument_returns_false_for_shell_injection(string value)
    {
        ArgumentValidator.IsSafeArgument(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("C:\\ProgramData\\AuraCorePro\\DriverBackup\\myfolder", "C:\\ProgramData\\AuraCorePro\\DriverBackup\\", true)]
    [InlineData("C:\\Windows\\System32\\config\\SAM", "C:\\ProgramData\\AuraCorePro\\DriverBackup\\", false)]
    [InlineData("..\\..\\escape", "C:\\ProgramData\\AuraCorePro\\DriverBackup\\", false)]
    public void IsPathUnderPrefix_guards_against_traversal(string path, string prefix, bool expected)
    {
        ArgumentValidator.IsPathUnderPrefix(path, prefix).Should().Be(expected);
    }

    [Theory]
    [InlineData("winmgmt", true)]
    [InlineData("BITS", true)]
    [InlineData("a_service-01", true)]
    [InlineData("service with space", false)]
    [InlineData("service;evil", false)]
    [InlineData("", false)]
    [MemberData(nameof(GetServiceNameTestData))]
    public void IsValidServiceName_applies_strict_regex(string name, bool expected)
    {
        ArgumentValidator.IsValidServiceName(name).Should().Be(expected);
    }

    public static TheoryData<string, bool> GetServiceNameTestData()
    {
        return new TheoryData<string, bool>
        {
            { new string('x', 257), false }, // Over 256 chars
        };
    }
}
