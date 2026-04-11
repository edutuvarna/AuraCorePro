using Xunit;
using AuraCore.Application.Shared;

namespace AuraCore.Tests.Unit;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsStdout()
    {
        string cmd, args;
        if (OperatingSystem.IsWindows())
        {
            cmd = "cmd.exe";
            args = "/c echo hello";
        }
        else
        {
            cmd = "/bin/sh";
            args = "-c \"echo hello\"";
        }

        var result = await ProcessRunner.RunAsync(cmd, args);
        Assert.True(result.Success);
        Assert.Contains("hello", result.Stdout);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidCommand_ReturnsError()
    {
        var result = await ProcessRunner.RunAsync("nonexistent_command_xyz_12345", "");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CommandExistsAsync_WellKnownCommand_ReturnsTrue()
    {
        // cmd exists on Windows, sh exists on Linux/macOS
        var cmd = OperatingSystem.IsWindows() ? "cmd" : "sh";
        var exists = await ProcessRunner.CommandExistsAsync(cmd);
        Assert.True(exists);
    }

    [Fact]
    public async Task CommandExistsAsync_NonexistentCommand_ReturnsFalse()
    {
        var exists = await ProcessRunner.CommandExistsAsync("zzz_fake_command_does_not_exist");
        Assert.False(exists);
    }
}
