using AuraCore.Infrastructure.PrivilegeIpc;
using AuraCore.Infrastructure.PrivilegeIpc.MacOS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class SMAppServiceRegistrarTests
{
    // ── Factory helper ────────────────────────────────────────────────────────

    private static SMAppServiceRegistrar Create(
        ISMAppServiceBridge? bridge = null,
        IBundleSignatureDetector? detector = null)
    {
        var logger = NullLogger<SMAppServiceRegistrar>.Instance;
        return new SMAppServiceRegistrar(
            bridge ?? Substitute.For<ISMAppServiceBridge>(),
            detector ?? Substitute.For<IBundleSignatureDetector>(),
            logger);
    }

    // ── Unsigned-bundle tests ─────────────────────────────────────────────────

    [Fact]
    public void RegisterHelper_returns_DevModeFallback_on_unsigned_bundle()
    {
        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(false);
        var bridge = Substitute.For<ISMAppServiceBridge>();

        var reg = Create(bridge, detector);
        var r = reg.RegisterHelper();

        // On macOS: DevModeFallback because unsigned; bridge not called.
        // On non-macOS: DevModeFallback because non-macOS host; bridge still not called.
        r.Outcome.Should().Be(RegistrationOutcome.DevModeFallback);
        bridge.DidNotReceive().RegisterDaemon(Arg.Any<string>());
    }

    // ── Signed-bundle / bridge delegation tests ───────────────────────────────

    [Fact]
    public void RegisterHelper_invokes_bridge_when_bundle_signed()
    {
        if (!OperatingSystem.IsMacOS()) return;  // bridge only called on macOS

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(true);
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.RegisterDaemon("pro.auracore.privhelper.plist").Returns(SMAppServiceStatus.Enabled);

        var reg = Create(bridge, detector);
        var r = reg.RegisterHelper();

        r.Outcome.Should().Be(RegistrationOutcome.Registered);
        r.Status.Should().Be(SMAppServiceStatus.Enabled);
        bridge.Received().RegisterDaemon("pro.auracore.privhelper.plist");
    }

    [Fact]
    public void RegisterHelper_returns_RequiresUserApproval_status()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(true);
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.RegisterDaemon(Arg.Any<string>()).Returns(SMAppServiceStatus.RequiresApproval);

        var r = Create(bridge, detector).RegisterHelper();
        r.Outcome.Should().Be(RegistrationOutcome.RequiresUserApproval);
        r.Status.Should().Be(SMAppServiceStatus.RequiresApproval);
    }

    [Fact]
    public void RegisterHelper_treats_NotSupported_as_DevModeFallback()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(true);
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.RegisterDaemon(Arg.Any<string>()).Returns(SMAppServiceStatus.NotSupported);

        var r = Create(bridge, detector).RegisterHelper();
        r.Outcome.Should().Be(RegistrationOutcome.DevModeFallback);
    }

    [Fact]
    public void RegisterHelper_treats_NotFound_as_Failed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(true);
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.RegisterDaemon(Arg.Any<string>()).Returns(SMAppServiceStatus.NotFound);

        var r = Create(bridge, detector).RegisterHelper();
        r.Outcome.Should().Be(RegistrationOutcome.Failed);
        r.Status.Should().Be(SMAppServiceStatus.NotFound);
    }

    [Fact]
    public void RegisterHelper_treats_NotRegistered_as_Failed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(true);
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.RegisterDaemon(Arg.Any<string>()).Returns(SMAppServiceStatus.NotRegistered);

        var r = Create(bridge, detector).RegisterHelper();
        r.Outcome.Should().Be(RegistrationOutcome.Failed);
        r.Status.Should().Be(SMAppServiceStatus.NotRegistered);
    }

    [Fact]
    public void RegisterHelper_returns_DevModeFallback_on_non_macos_regardless_of_detector()
    {
        // On non-macOS hosts, the registrar bails before even calling the detector.
        if (OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        var bridge   = Substitute.For<ISMAppServiceBridge>();

        // Do not configure detector to return anything — it shouldn't be called on non-macOS.
        var r = Create(bridge, detector).RegisterHelper();

        r.Outcome.Should().Be(RegistrationOutcome.DevModeFallback);
        bridge.DidNotReceive().RegisterDaemon(Arg.Any<string>());
    }

    // ── GetCurrentStatus ──────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentStatus_delegates_to_bridge()
    {
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.GetStatus("pro.auracore.privhelper.plist").Returns(SMAppServiceStatus.Enabled);
        var reg = Create(bridge: bridge);
        reg.GetCurrentStatus().Should().Be(SMAppServiceStatus.Enabled);
    }

    [Fact]
    public void GetCurrentStatus_returns_NotSupported_when_bridge_throws()
    {
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.GetStatus(Arg.Any<string>()).Throws(new InvalidOperationException("native died"));
        var reg = Create(bridge: bridge);
        reg.GetCurrentStatus().Should().Be(SMAppServiceStatus.NotSupported);
    }

    // ── DefaultBundleSignatureDetector ────────────────────────────────────────

    [Fact]
    public void DefaultBundleSignatureDetector_returns_false_on_non_macos()
    {
        // On Windows/Linux dev hosts the SecCode call can't succeed — returns false.
        if (OperatingSystem.IsMacOS()) return;
        var d = new DefaultBundleSignatureDetector();
        d.IsBundleProperlySignedWithTeamId().Should().BeFalse();
    }

    // ── DefaultSMAppServiceBridge ─────────────────────────────────────────────

    [Fact]
    public void DefaultSMAppServiceBridge_returns_NotSupported_on_non_macos()
    {
        if (OperatingSystem.IsMacOS()) return;
        var b = new DefaultSMAppServiceBridge(NullLogger<DefaultSMAppServiceBridge>.Instance);
        b.RegisterDaemon("anything").Should().Be(SMAppServiceStatus.NotSupported);
        b.GetStatus("anything").Should().Be(SMAppServiceStatus.NotSupported);
    }

    // ── Error resilience ──────────────────────────────────────────────────────

    [Fact]
    public void RegisterHelper_returns_Failed_when_bridge_RegisterDaemon_throws()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Returns(true);
        var bridge = Substitute.For<ISMAppServiceBridge>();
        bridge.RegisterDaemon(Arg.Any<string>()).Throws(new InvalidOperationException("boom"));

        var r = Create(bridge, detector).RegisterHelper();
        r.Outcome.Should().Be(RegistrationOutcome.Failed);
        r.ErrorMessage.Should().Contain("boom");
    }

    [Fact]
    public void RegisterHelper_returns_DevModeFallback_when_detector_throws()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var detector = Substitute.For<IBundleSignatureDetector>();
        detector.IsBundleProperlySignedWithTeamId().Throws(new InvalidOperationException("sig check died"));
        var bridge = Substitute.For<ISMAppServiceBridge>();

        var r = Create(bridge, detector).RegisterHelper();
        r.Outcome.Should().Be(RegistrationOutcome.DevModeFallback);
        bridge.DidNotReceive().RegisterDaemon(Arg.Any<string>());
    }
}

// ── DI wiring test (macOS-gated) ─────────────────────────────────────────────

public class SMAppServiceRegistrarDITests
{
    [Fact]
    public void AddPrivilegeIpc_on_osx_registers_SMAppServiceRegistrar_and_dependencies()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPrivilegeIpc();
        services.AddSingleton<AuraCore.Application.Interfaces.Platform.IHelperAvailabilityService>(
            Substitute.For<AuraCore.Application.Interfaces.Platform.IHelperAvailabilityService>());
        var sp = services.BuildServiceProvider();

        sp.GetService<SMAppServiceRegistrar>().Should().NotBeNull();
        sp.GetService<ISMAppServiceBridge>().Should().NotBeNull();
        sp.GetService<IBundleSignatureDetector>().Should().NotBeNull();
    }
}
