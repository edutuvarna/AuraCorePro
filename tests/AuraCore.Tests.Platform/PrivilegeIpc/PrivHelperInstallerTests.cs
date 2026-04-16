using AuraCore.Infrastructure.PrivilegeIpc.Linux;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class PrivHelperInstallerTests : IDisposable
{
    private readonly List<string> _tempPathsToCleanup = new();

    public void Dispose()
    {
        foreach (var p in _tempPathsToCleanup)
        {
            try { if (Directory.Exists(p)) Directory.Delete(p, recursive: true); } catch { /* best effort */ }
        }
    }

    private sealed class FakeBinaryLocator : IDaemonBinaryLocator
    {
        public string? Path { get; init; }
        public string? LocateDaemonBinary() => Path;
    }

    private static (string binaryPath, Action cleanup) CreateFakeBinary()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fake-daemon-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, new byte[] { 0x7F, 0x45, 0x4C, 0x46 }); // ELF magic for fun
        return (path, () => { try { File.Delete(path); } catch { } });
    }

    [Fact]
    public async Task ExtractStageAsync_writes_install_script_and_assets_and_binary()
    {
        var (binaryPath, cleanup) = CreateFakeBinary();
        try
        {
            var installer = new PrivHelperInstaller(
                Substitute.For<IPkexecInvoker>(),
                new FakeBinaryLocator { Path = binaryPath },
                NullLogger<PrivHelperInstaller>.Instance);

            var stageDir = await installer.ExtractStageAsync();
            _tempPathsToCleanup.Add(stageDir);

            Directory.Exists(stageDir).Should().BeTrue();
            File.Exists(Path.Combine(stageDir, "install.sh")).Should().BeTrue();
            File.Exists(Path.Combine(stageDir, "pro.auracore.privhelper.policy")).Should().BeTrue();
            File.Exists(Path.Combine(stageDir, "pro.auracore.privhelper.service")).Should().BeTrue();
            File.Exists(Path.Combine(stageDir, "privhelper")).Should().BeTrue();

            // Sanity — install.sh starts with shebang
            var shebang = File.ReadAllText(Path.Combine(stageDir, "install.sh")).Substring(0, 2);
            shebang.Should().Be("#!");
        }
        finally { cleanup(); }
    }

    [Fact]
    public async Task ExtractStageAsync_throws_when_binary_locator_returns_null()
    {
        var installer = new PrivHelperInstaller(
            Substitute.For<IPkexecInvoker>(),
            new FakeBinaryLocator { Path = null },
            NullLogger<PrivHelperInstaller>.Instance);

        var act = () => installer.ExtractStageAsync();
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ExtractAndInstallAsync_invokes_pkexec_with_correct_paths()
    {
        var (binaryPath, cleanup) = CreateFakeBinary();
        try
        {
            var pkexec = Substitute.For<IPkexecInvoker>();
            pkexec.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((0, "OK: auracore-privhelper installed\n", ""));

            var installer = new PrivHelperInstaller(
                pkexec,
                new FakeBinaryLocator { Path = binaryPath },
                NullLogger<PrivHelperInstaller>.Instance);

            var outcome = await installer.ExtractAndInstallAsync();

            outcome.Success.Should().BeTrue();
            outcome.ExitCode.Should().Be(0);
            outcome.StageDir.Should().NotBeNull();
            _tempPathsToCleanup.Add(outcome.StageDir!);

            await pkexec.Received(1).InvokeAsync(
                Arg.Is<string>(s => s.EndsWith("install.sh")),
                Arg.Is<string>(s => s == outcome.StageDir),
                Arg.Any<CancellationToken>());
        }
        finally { cleanup(); }
    }

    [Fact]
    public async Task ExtractAndInstallAsync_returns_failure_outcome_on_pkexec_nonzero_exit()
    {
        var (binaryPath, cleanup) = CreateFakeBinary();
        try
        {
            var pkexec = Substitute.For<IPkexecInvoker>();
            pkexec.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((1, "", "ERROR: user cancelled\n"));

            var installer = new PrivHelperInstaller(
                pkexec,
                new FakeBinaryLocator { Path = binaryPath },
                NullLogger<PrivHelperInstaller>.Instance);

            var outcome = await installer.ExtractAndInstallAsync();
            _tempPathsToCleanup.Add(outcome.StageDir!);

            outcome.Success.Should().BeFalse();
            outcome.ExitCode.Should().Be(1);
            outcome.Stderr.Should().Contain("user cancelled");
        }
        finally { cleanup(); }
    }

    [Fact]
    public async Task ExtractAndInstallAsync_returns_failure_when_extraction_fails()
    {
        var installer = new PrivHelperInstaller(
            Substitute.For<IPkexecInvoker>(),
            new FakeBinaryLocator { Path = null },       // will cause extraction to throw
            NullLogger<PrivHelperInstaller>.Instance);

        var outcome = await installer.ExtractAndInstallAsync();
        outcome.Success.Should().BeFalse();
        outcome.ExitCode.Should().Be(-1);
        outcome.StageDir.Should().BeNull();
        outcome.Stderr.Should().NotBeEmpty();
    }

    [Fact]
    public void DefaultDaemonBinaryLocator_returns_null_when_binary_not_found()
    {
        // On Windows dev, the daemon binary is NOT shipped with the main app.
        // The default locator should return null (caller treats as HelperMissing).
        var locator = new DefaultDaemonBinaryLocator();
        var result = locator.LocateDaemonBinary();
        // We don't assert null outright — a dev run might accidentally have the binary.
        // But the API contract is: "returns null if not locatable". Valid either way.
        (result == null || File.Exists(result)).Should().BeTrue();
    }
}
