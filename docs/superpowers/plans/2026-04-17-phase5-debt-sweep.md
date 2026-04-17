# Phase 5 Debt Sweep Mini-Wave Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close 8 high-impact carry-forward debt items from Phase 5 across 2 sub-waves — user-visible completions (ChatSection ReloadAsync UI wiring, Windows Named Pipe first-run install UX, WorstTemp live SMART data) plus stability/hygiene/Linux (UI test flake root cause, 6 skipped pilot-view render tests, Nginx ops tidy x2, GrubManager 3 deferred sudo hits).

**Architecture:** New branch `phase-5-debt-sweep` from `main` HEAD `cf5330c`. Two sub-waves with milestone commits; per-sub-wave rollback enabled. Sub-wave A = 3 user-visible tasks. Sub-wave B = 4 stability + ops + Linux tasks + 1 time-boxed flake investigation.

**Tech Stack:** .NET 8 · C# 12 · Avalonia UI 11 · xUnit + NSubstitute · PowerShell 5.1+ (UAC elevator) · WMI `MSStorageDriver_ATAPISmartData` (Windows) · `System.ServiceProcess.ServiceController` · live SSH to origin `165.227.170.3` (Nginx) · V2 design tokens only

**Spec reference:** `docs/superpowers/specs/2026-04-17-phase5-debt-sweep-design.md` (committed `ad86cf0`)

**Baseline:** Branch `phase-5-debt-sweep` from `main` `cf5330c`. 2141 passing + 6 skipped + 0 failed. Spec + this plan ride on top.

---

## File Structure

### Created

```
# Sub-wave A
src/UI/AuraCore.UI.Avalonia/Helpers/PrivilegedHelperInstaller.cs
src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegedHelperInstallDialog.axaml
src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegedHelperInstallDialog.axaml.cs
src/UI/AuraCore.UI.Avalonia/ViewModels/Dialogs/PrivilegedHelperInstallDialogVM.cs

tests/AuraCore.Tests.UI.Avalonia/Helpers/PrivilegedHelperInstallerTests.cs
tests/AuraCore.Tests.UI.Avalonia/Dialogs/PrivilegedHelperInstallDialogVMTests.cs
tests/AuraCore.Tests.UI.Avalonia/Chat/ChatSectionReloadWiringTests.cs
tests/AuraCore.Tests.UI.Avalonia/Dashboard/DiskHealthScannerSmartTests.cs

# Sub-wave B
tests/AuraCore.Tests.UI.Avalonia/Responsive/PilotModulesResponsiveTests_DIEnabled.cs  (if separate file wanted; else extend existing PilotModulesResponsiveTests.cs)
docs/deploy/2026-04-17-nginx-debt-cleanup.md                                           (session transcript for B3+B4)
```

### Modified

```
# Sub-wave A
src/UI/AuraCore.UI.Avalonia/Views/Controls/ChatSection.axaml.cs                        (A1 ReloadAsync wiring)
src/UI/AuraCore.UI.Avalonia/Helpers/DiskHealthScanner.cs                               (A3 WMI SMART augmentation)
src/UI/AuraCore.UI.Avalonia/App.axaml.cs                                               (A2 DI register PrivilegedHelperInstaller)
src/UI/AuraCore.UI.Avalonia/Views/Pages/DriverUpdaterView.axaml.cs                     (A2 gate first-run install prompt)
src/UI/AuraCore.UI.Avalonia/Views/Pages/DefenderManagerView.axaml.cs                   (A2 same)
src/UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml.cs                    (A2 same)
src/UI/AuraCore.UI.Avalonia/LocalizationService.cs                                     (A1 + A2 dialog/toast strings)
src/Core/AuraCore.Application/Settings/AppSettings.cs                                  (A2 PrivilegedHelperInstallState enum prop — check actual file location first)

# Sub-wave B
tests/AuraCore.Tests.UI.Avalonia/Avalonia/AvaloniaTestApplication.cs                   (B2 DI bootstrap)
tests/AuraCore.Tests.UI.Avalonia/Responsive/PilotModulesResponsiveTests.cs             (B2 un-skip 6 tests)
src/Service/AuraCore.PrivHelper.Linux/ActionWhitelist.cs                               (B5 new actions)
src/Service/AuraCore.PrivHelper.Linux/assets/pro.auracore.privhelper.policy            (B5 polkit mirror)
src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs                           (B5 migrate 3 sudo hits at lines ~199, ~320, ~373)
tests/AuraCore.Tests.Platform/PrivilegeIpc/ActionWhitelistTests.cs                     (B5 extend counts + entries)
tests/AuraCore.Tests.Platform/PrivilegeIpc/PrivHelperAssetsTests.cs                    (B5 extend counts)

# Live ops (not committed)
Live SSH: 165.227.170.3 — Nginx config edit (B3 + B4), transcript saved as docs/deploy/2026-04-17-nginx-debt-cleanup.md
```

---

# Sub-wave A — User-visible completions

## Task A1: ChatSection ReloadAsync UI wiring

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Controls/ChatSection.axaml.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` (2 new toast strings)
- Test: `tests/AuraCore.Tests.UI.Avalonia/Chat/ChatSectionReloadWiringTests.cs`

Rationale: `IAuraCoreLLM.ReloadAsync(newConfig, ct)` + `IsReloading` INPC contract landed in Phase 5.4 (`bd47650`). `ChatSection.axaml.cs` still has a stale comment saying the API doesn't exist and advises the user to restart. Replace with a real call.

- [ ] **Step 1: Read the current ChatSection to find the stale block**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
grep -n "restart\|Reload\|IAuraCoreLLM" src/UI/AuraCore.UI.Avalonia/Views/Controls/ChatSection.axaml.cs
```

Identify the method (likely a model-change handler or settings-applied callback) that currently shows a "restart to apply" toast/comment.

- [ ] **Step 2: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Chat/ChatSectionReloadWiringTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Engines;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Chat;

public class ChatSectionReloadWiringTests
{
    [Fact]
    public async Task When_model_changes_ChatSection_calls_ReloadAsync_and_not_restart_advice()
    {
        var llm = Substitute.For<IAuraCoreLLM>();
        llm.ReloadAsync(Arg.Any<LlmConfiguration>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);

        // Expose a pure helper on ChatSection that can be unit-tested without the full view:
        // public static async Task ApplyModelChangeAsync(IAuraCoreLLM llm, LlmConfiguration newConfig, CancellationToken ct)
        await AuraCore.UI.Avalonia.Views.Controls.ChatSection
            .ApplyModelChangeAsync(llm, new LlmConfiguration("models/qwen25-32b.gguf"), CancellationToken.None);

        await llm.Received(1).ReloadAsync(
            Arg.Is<LlmConfiguration>(c => c.ModelPath == "models/qwen25-32b.gguf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task When_ReloadAsync_is_cancelled_returns_without_throwing_to_caller()
    {
        var llm = Substitute.For<IAuraCoreLLM>();
        llm.ReloadAsync(Arg.Any<LlmConfiguration>(), Arg.Any<CancellationToken>())
           .Returns(_ => throw new System.OperationCanceledException());

        // Should swallow OCE inside ApplyModelChangeAsync and surface a cancelled-toast signal
        var result = await AuraCore.UI.Avalonia.Views.Controls.ChatSection
            .ApplyModelChangeAsync(llm, new LlmConfiguration("m.gguf"), CancellationToken.None);

        result.Status.Should().Be(ChatSectionReloadStatus.Cancelled);
    }

    [Fact]
    public async Task When_ReloadAsync_reports_concurrent_call_returns_Busy_status()
    {
        var llm = Substitute.For<IAuraCoreLLM>();
        llm.ReloadAsync(Arg.Any<LlmConfiguration>(), Arg.Any<CancellationToken>())
           .Returns(_ => throw new System.InvalidOperationException("already reloading"));

        var result = await AuraCore.UI.Avalonia.Views.Controls.ChatSection
            .ApplyModelChangeAsync(llm, new LlmConfiguration("m.gguf"), CancellationToken.None);

        result.Status.Should().Be(ChatSectionReloadStatus.Busy);
    }
}
```

Supporting enum added to the test fixture by hoisting logic into a static pure helper on `ChatSection`:

```csharp
// Part of ChatSection.axaml.cs (shared across code-behind + test):
public enum ChatSectionReloadStatus { Ok, Cancelled, Busy, Failed }
public readonly record struct ChatSectionReloadResult(ChatSectionReloadStatus Status, string? Error = null);
```

Decision: extracting the logic into a static helper is preferred over mocking the UI lifecycle — keeps tests fast + headless-safe.

- [ ] **Step 3: Verify the tests fail**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ChatSectionReloadWiringTests" --nologo 2>&1 | tail -8
```

Expected: build error — `ChatSection.ApplyModelChangeAsync` / `ChatSectionReloadStatus` / `ChatSectionReloadResult` don't exist.

**Note:** If `FluentAssertions` isn't referenced by this test project, substitute `Assert.Equal` / `Assert.Same` style (the Module test project uses plain xunit — check UI.Avalonia tests for their convention before committing).

- [ ] **Step 4: Extract the reload logic into a static helper on ChatSection**

In `src/UI/AuraCore.UI.Avalonia/Views/Controls/ChatSection.axaml.cs`, add near the top of the class (inside the existing `public partial class ChatSection`):

```csharp
public enum ChatSectionReloadStatus { Ok, Cancelled, Busy, Failed }
public readonly record struct ChatSectionReloadResult(ChatSectionReloadStatus Status, string? Error = null);

public static async Task<ChatSectionReloadResult> ApplyModelChangeAsync(
    IAuraCoreLLM llm, LlmConfiguration newConfig, CancellationToken ct)
{
    try
    {
        await llm.ReloadAsync(newConfig, ct);
        return new ChatSectionReloadResult(ChatSectionReloadStatus.Ok);
    }
    catch (OperationCanceledException)
    {
        return new ChatSectionReloadResult(ChatSectionReloadStatus.Cancelled);
    }
    catch (InvalidOperationException ex)
    {
        return new ChatSectionReloadResult(ChatSectionReloadStatus.Busy, ex.Message);
    }
    catch (Exception ex)
    {
        return new ChatSectionReloadResult(ChatSectionReloadStatus.Failed, ex.Message);
    }
}
```

Add the necessary `using AuraCore.Application.Interfaces.Engines;` at top if not present.

- [ ] **Step 5: Wire the helper into the existing model-change handler**

Locate the code-behind method handling model-selector changes. Replace the "restart to apply" advice block with:

```csharp
// Before (stale):
// ShowToast(LocalizationService.Get("chat.reload.restartAdvice"));
// log.Debug("IAuraCoreLLM has no Reload API; advise restart.");

// After:
var newConfig = new LlmConfiguration(resolvedModelPath);
_reloadCts?.Cancel();
_reloadCts = new CancellationTokenSource();

var result = await ApplyModelChangeAsync(_llm, newConfig, _reloadCts.Token);
var toastKey = result.Status switch
{
    ChatSectionReloadStatus.Ok        => "chat.reload.success",
    ChatSectionReloadStatus.Cancelled => "chat.reload.cancelled",
    ChatSectionReloadStatus.Busy      => "chat.reload.busy",
    ChatSectionReloadStatus.Failed    => "chat.reload.failed",
    _ => "chat.reload.failed"
};
ShowToast(LocalizationService.Get(toastKey));
```

Declare a field `private CancellationTokenSource? _reloadCts;` at the class scope if not already present. `_llm` is the existing `IAuraCoreLLM` field — grep to confirm the field name in the current file.

- [ ] **Step 6: Add localization keys**

In `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs`, add to EN + TR dictionaries:

```csharp
// EN
["chat.reload.success"]   = "Model reloaded",
["chat.reload.cancelled"] = "Model reload cancelled",
["chat.reload.busy"]      = "Another reload is in progress",
["chat.reload.failed"]    = "Model reload failed",

// TR (Turkish diacritics: ğ, ü, ş, ı, İ, ö, ç)
["chat.reload.success"]   = "Model yeniden yüklendi",
["chat.reload.cancelled"] = "Model yeniden yükleme iptal edildi",
["chat.reload.busy"]      = "Başka bir yeniden yükleme sürüyor",
["chat.reload.failed"]    = "Model yeniden yükleme başarısız",
```

If an older `chat.reload.restartAdvice` key exists and is no longer referenced after this change, remove it (search repo-wide to confirm zero references first).

- [ ] **Step 7: Optional — wire IsReloading spinner**

If ChatSection has a visible "thinking" indicator, bind its visibility to `_llm.IsReloading`. If there's no obvious spot to bind to, skip — the toast is the primary user signal. Note in report if skipped.

- [ ] **Step 8: Run tests + confirm**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~ChatSectionReloadWiringTests|FullyQualifiedName~LocalizationParity" --nologo 2>&1 | grep -E "Passed!|Failed"
```

Expected: 3 new tests pass; LocalizationParity still green.

- [ ] **Step 9: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Views/Controls/ChatSection.axaml.cs src/UI/AuraCore.UI.Avalonia/LocalizationService.cs tests/AuraCore.Tests.UI.Avalonia/Chat/
git commit -m "feat(debt-A1): wire IAuraCoreLLM.ReloadAsync into ChatSection UI"
```

---

## Task A3: WorstTemp live SMART data in DiskHealthScanner

**(A3 comes before A2 because A2 is larger and benefits from warm-up)**

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Helpers/DiskHealthScanner.cs`
- Test: `tests/AuraCore.Tests.UI.Avalonia/Dashboard/DiskHealthScannerSmartTests.cs`

Rationale: Phase 5.5 shipped DiskHealthSummaryCard with `WorstTemp = "—"` because `DriveInfo` has no temperature data. WMI `ROOT\WMI\MSStorageDriver_ATAPISmartData` exposes SMART attribute byte arrays; SMART attribute ID 0xC2 (or 0xBE depending on drive) is temperature in °C. Augment `ScanCore` to try WMI and fall back to "—" on failure.

- [ ] **Step 1: Read the current DiskHealthScanner**

```bash
cat src/UI/AuraCore.UI.Avalonia/Helpers/DiskHealthScanner.cs
```

Understand the current shape of `DiskHealthScanResult(Status, SmartBadge, WorstTemp)` and where `WorstTemp` is assigned today.

- [ ] **Step 2: Write the failing test**

Create `tests/AuraCore.Tests.UI.Avalonia/Dashboard/DiskHealthScannerSmartTests.cs`:

```csharp
using System.Collections.Generic;
using AuraCore.UI.Avalonia.Helpers;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dashboard;

public class DiskHealthScannerSmartTests
{
    // Pure parsing test — independent of live WMI.
    [Fact]
    public void ParseWorstTempCelsius_returns_highest_drive_temp_from_data()
    {
        // Two fake drives: drive0 at 38°C, drive1 at 45°C
        var samples = new List<DiskHealthScanner.SmartSample>
        {
            new("disk0", 38),
            new("disk1", 45),
        };
        var worst = DiskHealthScanner.PickWorstTempCelsius(samples);
        Assert.Equal(45, worst);
    }

    [Fact]
    public void ParseWorstTempCelsius_on_empty_returns_null()
    {
        var worst = DiskHealthScanner.PickWorstTempCelsius(new List<DiskHealthScanner.SmartSample>());
        Assert.Null(worst);
    }

    [Fact]
    public void FormatWorstTemp_null_returns_placeholder()
    {
        Assert.Equal("—", DiskHealthScanner.FormatWorstTemp(null));
    }

    [Fact]
    public void FormatWorstTemp_numeric_returns_with_celsius_suffix()
    {
        Assert.Equal("45°C", DiskHealthScanner.FormatWorstTemp(45));
    }

    [Fact]
    public void ScanAsync_gracefully_falls_back_when_WMI_throws()
    {
        // Inject a throwing SmartProbe; ScanAsync should swallow and return placeholder WorstTemp.
        var probe = new ThrowingSmartProbe();
        var result = DiskHealthScanner.ScanCore(probe);
        Assert.Equal("—", result.WorstTemp);
    }

    private sealed class ThrowingSmartProbe : DiskHealthScanner.ISmartProbe
    {
        public IReadOnlyList<DiskHealthScanner.SmartSample> Sample() => throw new System.Exception("boom");
    }
}
```

Supporting types introduced to DiskHealthScanner:

```csharp
// New in DiskHealthScanner.cs:
public readonly record struct SmartSample(string DeviceId, int TempCelsius);
public interface ISmartProbe { IReadOnlyList<SmartSample> Sample(); }
```

- [ ] **Step 3: Verify test fails**

```bash
dotnet build tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --nologo 2>&1 | tail -5
```

Expected: build errors for `ISmartProbe`, `SmartSample`, `PickWorstTempCelsius`, `FormatWorstTemp`, `ScanCore(probe)` overload.

- [ ] **Step 4: Implement the parsing + pure helpers**

Add to `DiskHealthScanner.cs`:

```csharp
public readonly record struct SmartSample(string DeviceId, int TempCelsius);

public interface ISmartProbe
{
    IReadOnlyList<SmartSample> Sample();
}

public static int? PickWorstTempCelsius(IReadOnlyList<SmartSample> samples)
{
    if (samples is null || samples.Count == 0) return null;
    int worst = int.MinValue;
    foreach (var s in samples)
    {
        if (s.TempCelsius > worst) worst = s.TempCelsius;
    }
    return worst == int.MinValue ? null : worst;
}

public static string FormatWorstTemp(int? celsius)
    => celsius.HasValue ? $"{celsius.Value}°C" : "—";
```

- [ ] **Step 5: Implement the WMI-backed SmartProbe + wire into ScanCore**

Add to `DiskHealthScanner.cs`:

```csharp
// Uses System.Management — already available as BCL on .NET 8 Windows target.
// If not, add <PackageReference Include="System.Management" Version="8.0.0" /> to the UI csproj.
// Check first: grep src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj for System.Management.
private sealed class WmiSmartProbe : ISmartProbe
{
    public IReadOnlyList<SmartSample> Sample()
    {
        var list = new List<SmartSample>();
        if (!OperatingSystem.IsWindows()) return list;

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "root\\WMI",
                "SELECT InstanceName, VendorSpecific FROM MSStorageDriver_ATAPISmartData");
            foreach (System.Management.ManagementObject o in searcher.Get())
            {
                try
                {
                    var instanceName = o["InstanceName"]?.ToString() ?? "";
                    var bytes = o["VendorSpecific"] as byte[];
                    if (bytes is null || bytes.Length < 6) continue;

                    // SMART attribute records start at offset 2 (skip 2-byte header).
                    // Each record is 12 bytes: [AttrId, Flags, Flags, Current, Worst, RawBytes(6), Reserved].
                    for (int i = 2; i + 12 <= bytes.Length; i += 12)
                    {
                        var attrId = bytes[i];
                        if (attrId == 0xC2 /* temperature */ || attrId == 0xBE /* airflow temp */)
                        {
                            var rawLow = bytes[i + 5]; // first raw byte typically the current temp
                            if (rawLow is > 0 and < 120)
                            {
                                list.Add(new SmartSample(instanceName, rawLow));
                                break; // one temp reading per disk is enough
                            }
                        }
                    }
                }
                catch { /* one bad drive shouldn't break the whole scan */ }
            }
        }
        catch
        {
            // WMI denied, query malformed on this Windows version, etc. — return empty list.
            return list;
        }
        return list;
    }
}

// Overload of ScanCore for DI / testing:
public static DiskHealthScanResult ScanCore(ISmartProbe probe)
{
    var driveResult = ScanDrivesCore(); // existing private method — extract if it's inline today
    int? worst = null;
    try { worst = PickWorstTempCelsius(probe.Sample()); } catch { }
    return driveResult with { WorstTemp = FormatWorstTemp(worst) };
}

// Public entry-point continues to use WmiSmartProbe by default:
public static async Task<DiskHealthScanResult> ScanAsync(CancellationToken ct = default)
{
    return await Task.Run(() => ScanCore(new WmiSmartProbe()), ct);
}
```

Refactor the existing `ScanAsync` / `ScanCore` if they currently assign `WorstTemp = "—"` inline — pull the drive-scan logic into a parameterless `ScanDrivesCore()` that the new `ScanCore(ISmartProbe)` wraps.

**Package check**: `grep "System.Management" src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj` — add the PackageReference if missing:

```xml
<PackageReference Include="System.Management" Version="8.0.0" />
```

`System.Management` is a Microsoft-published BCL-adjacent package, consistent with Phase 5's discipline.

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~DiskHealthScannerSmartTests" --nologo 2>&1 | grep -E "Passed!|Failed"
```

Expected: 5 new tests pass.

- [ ] **Step 7: Full-solution test + manual smoke (optional)**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: all 8 assemblies pass. Pre-existing WinAppSDK Desktop error is OK.

Optional manual smoke: launch the app, open Dashboard, confirm Disk Health card shows a real °C value on a SMART-capable drive (or stays at "—" on non-admin / non-SMART systems — both are valid).

- [ ] **Step 8: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Helpers/DiskHealthScanner.cs src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj tests/AuraCore.Tests.UI.Avalonia/Dashboard/DiskHealthScannerSmartTests.cs
git commit -m "feat(debt-A3): WMI SMART worst-temp wiring for DiskHealthScanner"
```

---

## Task A2: Windows Named Pipe first-run install UX

**Files:**
- Create: `src/UI/AuraCore.UI.Avalonia/Helpers/PrivilegedHelperInstaller.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/ViewModels/Dialogs/PrivilegedHelperInstallDialogVM.cs`
- Create: `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegedHelperInstallDialog.axaml` + `.cs`
- Modify: `src/UI/AuraCore.UI.Avalonia/App.axaml.cs` (DI registration)
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DriverUpdaterView.axaml.cs` (gate privileged action)
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DefenderManagerView.axaml.cs` (same)
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml.cs` (same)
- Modify: `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` (dialog strings)
- Modify: `src/Core/AuraCore.Application/Settings/AppSettings.cs` (helper-install state enum; check actual path first — grep `AppSettings`)
- Test: `tests/AuraCore.Tests.UI.Avalonia/Helpers/PrivilegedHelperInstallerTests.cs`
- Test: `tests/AuraCore.Tests.UI.Avalonia/Dialogs/PrivilegedHelperInstallDialogVMTests.cs`

Rationale: End users can't use Driver/Defender/Service write features without `scripts/install-privileged-service.ps1` run as admin. Provide an in-app consent dialog + UAC-elevated script invocation + pipe-availability poll. Triggered lazily on first privileged action, not on app launch.

- [ ] **Step 1: Detect current AppSettings location and shape**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
find src -iname "AppSettings*.cs" -not -path "*bin*" -not -path "*obj*" 2>/dev/null | head -5
grep -n "PrivilegedHelper\|helperInstall" src -r --include="*.cs" 2>/dev/null | head -5
```

If no existing `AppSettings` path fits (e.g., `src/Core/AuraCore.Application/Settings/AppSettings.cs` doesn't exist), add the new enum + property to whichever settings container is canonical. If it's in `AuraCore.UI.Avalonia`, put it there.

- [ ] **Step 2: Write failing tests for PrivilegedHelperInstaller**

Create `tests/AuraCore.Tests.UI.Avalonia/Helpers/PrivilegedHelperInstallerTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

public class PrivilegedHelperInstallerTests
{
    [Fact]
    public async Task IsInstalledAsync_returns_true_when_pipe_probe_succeeds()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        var installer = new PrivilegedHelperInstaller(probe, _ => Task.FromResult(true));
        var installed = await installer.IsInstalledAsync(CancellationToken.None);
        Assert.True(installed);
    }

    [Fact]
    public async Task IsInstalledAsync_returns_false_when_pipe_probe_fails()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(false));

        var installer = new PrivilegedHelperInstaller(probe, _ => Task.FromResult(true));
        var installed = await installer.IsInstalledAsync(CancellationToken.None);
        Assert.False(installed);
    }

    [Fact]
    public async Task InstallAsync_invokes_elevator_then_polls_pipe()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        bool elevatorCalled = false;
        Task<bool> Elevator(string scriptPath)
        {
            elevatorCalled = true;
            return Task.FromResult(true);
        }

        var installer = new PrivilegedHelperInstaller(probe, Elevator);
        var outcome = await installer.InstallAsync(CancellationToken.None);

        Assert.True(elevatorCalled);
        Assert.Equal(PrivilegedHelperInstallOutcome.Success, outcome);
    }

    [Fact]
    public async Task InstallAsync_when_pipe_never_comes_up_returns_Timeout()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(false));

        var installer = new PrivilegedHelperInstaller(probe, _ => Task.FromResult(true));
        var outcome = await installer.InstallAsync(CancellationToken.None);

        Assert.Equal(PrivilegedHelperInstallOutcome.Timeout, outcome);
    }

    [Fact]
    public async Task InstallAsync_when_elevator_declines_returns_UserCancelled()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(false));

        var installer = new PrivilegedHelperInstaller(probe, _ => Task.FromResult(false));
        var outcome = await installer.InstallAsync(CancellationToken.None);

        Assert.Equal(PrivilegedHelperInstallOutcome.UserCancelled, outcome);
    }
}
```

- [ ] **Step 3: Implement PrivilegedHelperInstaller**

Create `src/UI/AuraCore.UI.Avalonia/Helpers/PrivilegedHelperInstaller.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Helpers;

public enum PrivilegedHelperInstallOutcome
{
    Success,
    UserCancelled,
    Timeout,
    Failed
}

public interface IPipeProbe
{
    Task<bool> CanConnectAsync(int timeoutMs, CancellationToken ct);
}

public sealed class NamedPipeProbe : IPipeProbe
{
    private readonly string _pipeName;

    public NamedPipeProbe(string pipeName = "AuraCorePro") { _pipeName = pipeName; }

    public async Task<bool> CanConnectAsync(int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(timeoutMs, ct);
            return client.IsConnected;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class PrivilegedHelperInstaller
{
    private readonly IPipeProbe _probe;
    private readonly Func<string, Task<bool>> _elevatorInvoke;
    private const int PipeConnectTimeoutMs = 2000;
    private const int PostInstallPollBudgetMs = 5000;
    private const int PostInstallPollStepMs = 200;

    public PrivilegedHelperInstaller(IPipeProbe probe, Func<string, Task<bool>> elevatorInvoke)
    {
        _probe = probe;
        _elevatorInvoke = elevatorInvoke;
    }

    public Task<bool> IsInstalledAsync(CancellationToken ct)
        => _probe.CanConnectAsync(PipeConnectTimeoutMs, ct);

    public async Task<PrivilegedHelperInstallOutcome> InstallAsync(CancellationToken ct)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "install-privileged-service.ps1");
        if (!File.Exists(scriptPath))
        {
            return PrivilegedHelperInstallOutcome.Failed;
        }

        bool elevatorOk = false;
        try { elevatorOk = await _elevatorInvoke(scriptPath); }
        catch { return PrivilegedHelperInstallOutcome.Failed; }

        if (!elevatorOk) return PrivilegedHelperInstallOutcome.UserCancelled;

        // Poll the pipe for PostInstallPollBudgetMs (200ms steps)
        var deadline = DateTime.UtcNow.AddMilliseconds(PostInstallPollBudgetMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await _probe.CanConnectAsync(PostInstallPollStepMs, ct))
                return PrivilegedHelperInstallOutcome.Success;
            await Task.Delay(PostInstallPollStepMs, ct);
        }

        return PrivilegedHelperInstallOutcome.Timeout;
    }

    public static Task<bool> DefaultElevatorInvoke(string scriptPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                Verb = "runas",
                UseShellExecute = true, // required for runas
                CreateNoWindow = false
            };
            using var proc = Process.Start(psi);
            if (proc is null) return Task.FromResult(false);
            proc.WaitForExit();
            return Task.FromResult(proc.ExitCode == 0);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User clicked "No" on the UAC prompt — Win32Exception with code 1223 (ERROR_CANCELLED)
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
```

- [ ] **Step 4: Implement PrivilegedHelperInstallDialogVM**

Create `src/UI/AuraCore.UI.Avalonia/ViewModels/Dialogs/PrivilegedHelperInstallDialogVM.cs`:

```csharp
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;

namespace AuraCore.UI.Avalonia.ViewModels.Dialogs;

public sealed class PrivilegedHelperInstallDialogVM : INotifyPropertyChanged
{
    private readonly PrivilegedHelperInstaller _installer;
    private bool _isInstalling;
    private string _statusText = "";

    public PrivilegedHelperInstallDialogVM(PrivilegedHelperInstaller installer)
    {
        _installer = installer;
        StatusText = LocalizationService.Get("privhelper.dialog.intro");
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set { if (_isInstalling != value) { _isInstalling = value; Raise(); } }
    }

    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText != value) { _statusText = value; Raise(); } }
    }

    public async Task<PrivilegedHelperInstallOutcome> InstallAsync(CancellationToken ct)
    {
        IsInstalling = true;
        try
        {
            StatusText = LocalizationService.Get("privhelper.dialog.installing");
            var outcome = await _installer.InstallAsync(ct);
            StatusText = outcome switch
            {
                PrivilegedHelperInstallOutcome.Success       => LocalizationService.Get("privhelper.dialog.success"),
                PrivilegedHelperInstallOutcome.UserCancelled => LocalizationService.Get("privhelper.dialog.userCancelled"),
                PrivilegedHelperInstallOutcome.Timeout       => LocalizationService.Get("privhelper.dialog.timeout"),
                _                                            => LocalizationService.Get("privhelper.dialog.failed")
            };
            return outcome;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 5: Write VM tests**

Create `tests/AuraCore.Tests.UI.Avalonia/Dialogs/PrivilegedHelperInstallDialogVMTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels.Dialogs;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Dialogs;

public class PrivilegedHelperInstallDialogVMTests
{
    [Fact]
    public async Task InstallAsync_sets_IsInstalling_true_during_and_false_after()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));
        var installer = new PrivilegedHelperInstaller(probe, _ => Task.FromResult(true));
        var vm = new PrivilegedHelperInstallDialogVM(installer);

        Assert.False(vm.IsInstalling);
        var task = vm.InstallAsync(CancellationToken.None);
        // During is tricky in sync test; mainly assert post-state.
        await task;
        Assert.False(vm.IsInstalling);
    }

    [Fact]
    public async Task InstallAsync_updates_StatusText_to_success_on_ok()
    {
        var probe = Substitute.For<IPipeProbe>();
        probe.CanConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));
        var installer = new PrivilegedHelperInstaller(probe, _ => Task.FromResult(true));
        var vm = new PrivilegedHelperInstallDialogVM(installer);

        await vm.InstallAsync(CancellationToken.None);
        Assert.Contains("success", vm.StatusText, System.StringComparison.OrdinalIgnoreCase);
    }
}
```

**Note**: The "success" string check uses case-insensitive substring because Turkish locale may render differently. Alternative: check `vm.StatusText` equals `LocalizationService.Get("privhelper.dialog.success")` exact match. Pick whichever is more brittle-proof in practice.

- [ ] **Step 6: Implement the dialog AXAML + code-behind**

Create `src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegedHelperInstallDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AuraCore.UI.Avalonia.Views.Dialogs.PrivilegedHelperInstallDialog"
        Title="AuraCore Privileged Helper"
        Width="480" Height="280"
        WindowStartupLocation="CenterOwner"
        CanResize="False">
  <Grid Margin="24" RowDefinitions="Auto,*,Auto">
    <TextBlock Text="{Binding Title}" FontWeight="SemiBold"
               FontSize="{DynamicResource FontSizeHeading}"/>
    <TextBlock Grid.Row="1" Text="{Binding StatusText}"
               FontSize="{DynamicResource FontSizeBody}"
               TextWrapping="Wrap" Margin="0,12,0,0"/>
    <StackPanel Grid.Row="2" Orientation="Horizontal"
                HorizontalAlignment="Right" Spacing="8" Margin="0,16,0,0">
      <Button x:Name="CancelBtn"  Content="{Binding CancelLabel}"  Click="Cancel_Click"/>
      <Button x:Name="InstallBtn" Content="{Binding InstallLabel}" Click="Install_Click"
              Classes="accent"/>
    </StackPanel>
  </Grid>
</Window>
```

Create `PrivilegedHelperInstallDialog.axaml.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels.Dialogs;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class PrivilegedHelperInstallDialog : Window
{
    private readonly PrivilegedHelperInstallDialogVM _vm;
    private CancellationTokenSource? _cts;

    public PrivilegedHelperInstallOutcome? Outcome { get; private set; }

    public PrivilegedHelperInstallDialog(PrivilegedHelperInstaller installer)
    {
        InitializeComponent();
        _vm = new PrivilegedHelperInstallDialogVM(installer);
        DataContext = _vm;
    }

    private async void Install_Click(object? sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        InstallBtn.IsEnabled = false;
        try
        {
            Outcome = await _vm.InstallAsync(_cts.Token);
        }
        finally
        {
            InstallBtn.IsEnabled = true;
        }
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Outcome = PrivilegedHelperInstallOutcome.UserCancelled;
        Close();
    }
}
```

- [ ] **Step 7: Add localization keys**

In `LocalizationService.cs` add EN + TR pairs:

```csharp
// EN
["privhelper.dialog.title"]         = "Install Privileged Helper",
["privhelper.dialog.intro"]         = "Driver, Defender, and Service write features require a background helper service that runs with elevated privileges. Installing will prompt for admin access.",
["privhelper.dialog.installLabel"]  = "Install",
["privhelper.dialog.cancelLabel"]   = "Cancel",
["privhelper.dialog.installing"]    = "Installing… please approve the UAC prompt.",
["privhelper.dialog.success"]       = "Privileged helper installed successfully.",
["privhelper.dialog.userCancelled"] = "Install cancelled.",
["privhelper.dialog.timeout"]       = "Helper was installed but pipe did not come up within 5 seconds.",
["privhelper.dialog.failed"]        = "Install failed. Check Event Viewer for details.",
["privhelper.notInstalled.toast"]   = "Privileged helper not installed — click here to install.",

// TR
["privhelper.dialog.title"]         = "Yetkili Yardımcıyı Kur",
["privhelper.dialog.intro"]         = "Sürücü, Defender ve Servis yazma özellikleri, yükseltilmiş yetkilerle çalışan bir yardımcı servise ihtiyaç duyar. Kurulum yönetici onayı ister.",
["privhelper.dialog.installLabel"]  = "Kur",
["privhelper.dialog.cancelLabel"]   = "İptal",
["privhelper.dialog.installing"]    = "Kuruluyor… UAC istemini onaylayın.",
["privhelper.dialog.success"]       = "Yetkili yardımcı başarıyla kuruldu.",
["privhelper.dialog.userCancelled"] = "Kurulum iptal edildi.",
["privhelper.dialog.timeout"]       = "Yardımcı kuruldu ancak 5 saniye içinde hazır olmadı.",
["privhelper.dialog.failed"]        = "Kurulum başarısız. Ayrıntılar için Olay Görüntüleyici'yi kontrol edin.",
["privhelper.notInstalled.toast"]   = "Yetkili yardımcı kurulu değil — kurmak için tıkla.",
```

Add `Title` / `CancelLabel` / `InstallLabel` bindings to the VM if they're bound from XAML:

```csharp
public string Title        => LocalizationService.Get("privhelper.dialog.title");
public string CancelLabel  => LocalizationService.Get("privhelper.dialog.cancelLabel");
public string InstallLabel => LocalizationService.Get("privhelper.dialog.installLabel");
```

- [ ] **Step 8: Register services in App.axaml.cs**

Find the DI bootstrap block and add:

```csharp
services.AddSingleton<IPipeProbe>(new NamedPipeProbe("AuraCorePro"));
services.AddSingleton<PrivilegedHelperInstaller>(sp =>
    new PrivilegedHelperInstaller(
        sp.GetRequiredService<IPipeProbe>(),
        PrivilegedHelperInstaller.DefaultElevatorInvoke));
```

- [ ] **Step 9: Wire first-run gate into privileged-action buttons**

In each of `DriverUpdaterView.axaml.cs`, `DefenderManagerView.axaml.cs`, `ServiceManagerView.axaml.cs`, at the top of every handler that calls `_module.XxxAsync()` (the privileged write paths), insert:

```csharp
// Before any privileged action:
var installer = App.Services?.GetService<PrivilegedHelperInstaller>();
if (installer is not null && !await installer.IsInstalledAsync(CancellationToken.None))
{
    var dialog = new PrivilegedHelperInstallDialog(installer);
    await dialog.ShowDialog(this.VisualRoot as Avalonia.Controls.Window ?? new Window());
    if (dialog.Outcome is not PrivilegedHelperInstallOutcome.Success)
    {
        // User declined OR install failed — show toast and bail.
        ShowStatus(LocalizationService.Get("privhelper.notInstalled.toast"));
        return;
    }
}
// proceed with the privileged call...
```

Adapt `ShowStatus` to each view's status surface (existing pattern from Phase 5.5). `this.VisualRoot` resolves the hosting window for modal parenting — adjust if the views embed differently.

- [ ] **Step 10: Run all tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~PrivilegedHelperInstallerTests|FullyQualifiedName~PrivilegedHelperInstallDialogVMTests|FullyQualifiedName~LocalizationParity" --nologo 2>&1 | grep -E "Passed!|Failed"
```

Expected: 7+ new tests pass, parity still green.

- [ ] **Step 11: Full-solution test**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: 8 passed, 0 failed (UI flake may surface once — retry if so; noted for Task B1).

- [ ] **Step 12: Commit**

```bash
git add src/UI/AuraCore.UI.Avalonia/Helpers/PrivilegedHelperInstaller.cs \
        src/UI/AuraCore.UI.Avalonia/ViewModels/Dialogs/ \
        src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegedHelperInstallDialog.axaml* \
        src/UI/AuraCore.UI.Avalonia/App.axaml.cs \
        src/UI/AuraCore.UI.Avalonia/Views/Pages/DriverUpdaterView.axaml.cs \
        src/UI/AuraCore.UI.Avalonia/Views/Pages/DefenderManagerView.axaml.cs \
        src/UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml.cs \
        src/UI/AuraCore.UI.Avalonia/LocalizationService.cs \
        tests/AuraCore.Tests.UI.Avalonia/Helpers/ tests/AuraCore.Tests.UI.Avalonia/Dialogs/
git commit -m "feat(debt-A2): first-run UAC install prompt for Windows privileged helper"
```

---

## Task A-milestone: Sub-wave A close

- [ ] **Step 1: Full solution test green**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: 8 `Passed!` lines, no `Failed!`. If UI.Avalonia flakes once, retry.

- [ ] **Step 2: Ceremonial commit**

```bash
git commit --allow-empty -m "milestone: Phase 5 debt sweep sub-wave A (user-visible) COMPLETE"
```

---

# Sub-wave B — Stability + ops hygiene + Linux whitelist

## Task B5: GrubManager 3 deferred sudo hits

**(B5 first because smallest, most mechanical)**

**Files:**
- Modify: `src/Service/AuraCore.PrivHelper.Linux/ActionWhitelist.cs`
- Modify: `src/Service/AuraCore.PrivHelper.Linux/assets/pro.auracore.privhelper.policy`
- Modify: `src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs` (3 sudo sites at approx lines 199, 320, 373)
- Modify: `tests/AuraCore.Tests.Platform/PrivilegeIpc/ActionWhitelistTests.cs`
- Modify: `tests/AuraCore.Tests.Platform/PrivilegeIpc/PrivHelperAssetsTests.cs`

- [ ] **Step 1: Find the 3 TODO markers + inspect call sites**

```bash
grep -n "TODO(phase-5.2.1)\|TODO(phase-5\\.2\\.1)" src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs
grep -n "sudo" src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs | head -10
```

Expected: 3 TODO markers with `sudo` usage nearby. Understand what each invokes (likely `grub-mkconfig -o /boot/grub/grub.cfg` and `cp -a /etc/default/grub /etc/default/grub.bak.<ts>`).

- [ ] **Step 2: Add 2 new actions to the Linux whitelist**

Edit `src/Service/AuraCore.PrivHelper.Linux/ActionWhitelist.cs`. Follow the existing pattern (observed in Phase 5.5 Task 14's `symlink.create` addition). Add entries similar to:

```csharp
// Phase debt-B5 — GrubManager
["grub-mkconfig"] = new GrubMkconfigValidator(),
["backup-etc-grub"] = new BackupEtcGrubValidator(),
```

Create dedicated validator classes in `src/Service/AuraCore.PrivHelper.Linux/Validators/GrubMkconfigValidator.cs` and `BackupEtcGrubValidator.cs` — follow the `SymlinkArgvValidator` shape (strict: no shell metacharacters, fixed-flag checks, path canonicalization).

```csharp
// GrubMkconfigValidator.cs
using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

public sealed class GrubMkconfigValidator : IArgvValidator
{
    public string Executable => "/usr/sbin/grub-mkconfig";

    public bool IsAllowed(string[] argv, out string? reason)
    {
        reason = null;
        if (argv.Length != 2) { reason = "expected 2 args: -o <path>"; return false; }
        if (argv[0] != "-o")  { reason = "first arg must be -o"; return false; }
        if (argv[1] != "/boot/grub/grub.cfg") { reason = "output path locked to /boot/grub/grub.cfg"; return false; }
        return true;
    }
}
```

```csharp
// BackupEtcGrubValidator.cs
using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

public sealed class BackupEtcGrubValidator : IArgvValidator
{
    public string Executable => "/bin/cp";
    // Backup filename pattern: /etc/default/grub.bak.YYYYMMDD-HHMMSS
    private static readonly Regex BackupPattern = new(
        @"^/etc/default/grub\.bak\.[0-9]{8}-[0-9]{6}$", RegexOptions.Compiled);

    public bool IsAllowed(string[] argv, out string? reason)
    {
        reason = null;
        if (argv.Length != 3) { reason = "expected 3 args: -a <src> <dst>"; return false; }
        if (argv[0] != "-a")  { reason = "first arg must be -a"; return false; }
        if (argv[1] != "/etc/default/grub") { reason = "source locked to /etc/default/grub"; return false; }
        if (!BackupPattern.IsMatch(argv[2]))
        {
            reason = "destination must match /etc/default/grub.bak.YYYYMMDD-HHMMSS";
            return false;
        }
        return true;
    }
}
```

(Adjust `IArgvValidator` interface name if the current codebase uses a different interface — observed from `SymlinkArgvValidator.cs` which Phase 5.5 added.)

- [ ] **Step 3: Mirror in polkit policy**

Open `src/Service/AuraCore.PrivHelper.Linux/assets/pro.auracore.privhelper.policy`. Add two new action stanzas matching the existing `auth_admin_keep` pattern:

```xml
<action id="pro.auracore.privhelper.grub-mkconfig">
  <description>Regenerate GRUB config (grub-mkconfig -o /boot/grub/grub.cfg)</description>
  <message>Authentication is required to regenerate the GRUB bootloader config.</message>
  <defaults>
    <allow_any>auth_admin_keep</allow_any>
    <allow_inactive>auth_admin_keep</allow_inactive>
    <allow_active>auth_admin_keep</allow_active>
  </defaults>
</action>

<action id="pro.auracore.privhelper.backup-etc-grub">
  <description>Back up /etc/default/grub</description>
  <message>Authentication is required to back up the GRUB defaults file.</message>
  <defaults>
    <allow_any>auth_admin_keep</allow_any>
    <allow_inactive>auth_admin_keep</allow_inactive>
    <allow_active>auth_admin_keep</allow_active>
  </defaults>
</action>
```

- [ ] **Step 4: Migrate 3 sudo call sites in GrubManagerModule**

For each `TODO(phase-5.2.1)` marker around lines 199, 320, 373 in `GrubManagerModule.cs`, replace the `sudo`-based call:

```csharp
// Before:
// var r = await ProcessRunner.RunAsync("sudo", "-n grub-mkconfig -o /boot/grub/grub.cfg", ct);

// After:
var r = await _shell.RunPrivilegedAsync(
    new PrivilegedCommand("grub-mkconfig", "grub-mkconfig", new[] { "-o", "/boot/grub/grub.cfg" }, TimeoutSeconds: 60),
    ct);
```

And for the backup site:

```csharp
// Before:
// var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
// var r = await ProcessRunner.RunAsync("sudo", $"-n cp -a /etc/default/grub /etc/default/grub.bak.{ts}", ct);

// After:
var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
var r = await _shell.RunPrivilegedAsync(
    new PrivilegedCommand("backup-etc-grub", "cp", new[] { "-a", "/etc/default/grub", $"/etc/default/grub.bak.{ts}" }, TimeoutSeconds: 15),
    ct);
```

Verify `_shell` is `IShellCommandService` — GrubManagerModule should already have it from Phase 5.2.1 (grep to confirm). If not, add optional ctor overload following the Phase 5.5 pattern.

Delete the `TODO(phase-5.2.1)` comment lines after migrating.

- [ ] **Step 5: Update ActionWhitelist + asset tests**

In `tests/AuraCore.Tests.Platform/PrivilegeIpc/ActionWhitelistTests.cs`, the Linux whitelist count check will need updating (from 7 → 9 if symlink.create made it 7). Grep the current count and bump by 2:

```csharp
// Before (example shape):
// Assert.Equal(7, ActionWhitelist.Linux.All.Count);
// After:
Assert.Equal(9, ActionWhitelist.Linux.All.Count);
Assert.Contains("grub-mkconfig", ActionWhitelist.Linux.All);
Assert.Contains("backup-etc-grub", ActionWhitelist.Linux.All);
```

In `tests/AuraCore.Tests.Platform/PrivilegeIpc/PrivHelperAssetsTests.cs`, the polkit assertion count similarly needs +2. Update the count constant and extend the expected-id list.

- [ ] **Step 6: Run tests**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

Expected: all green. GrubManager-specific tests (if any) may need updating — grep for any.

- [ ] **Step 7: Commit**

```bash
git add src/Service/AuraCore.PrivHelper.Linux/ActionWhitelist.cs \
        src/Service/AuraCore.PrivHelper.Linux/Validators/ \
        src/Service/AuraCore.PrivHelper.Linux/assets/pro.auracore.privhelper.policy \
        src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs \
        tests/AuraCore.Tests.Platform/PrivilegeIpc/
git commit -m "feat(debt-B5): GrubManager 3 deferred sudo hits migrated via whitelist+polkit"
```

---

## Task B3+B4: Nginx ops tidy (live SSH to origin 165.227.170.3)

**Files:**
- Create: `docs/deploy/2026-04-17-nginx-debt-cleanup.md` (session transcript)
- Live ops on origin Nginx at `165.227.170.3` (NOT committed to repo)

**IMPORTANT:** These are live production SSH operations. Single session handles both B3 and B4.

- [ ] **Step 1: SSH connect + inspect current state**

```bash
ssh root@165.227.170.3
ls -la /etc/nginx/sites-enabled/ | grep -i "auracore"
ls -la /etc/nginx/sites-available/ | grep -i "auracore"
grep -n "Strict-Transport-Security" /etc/nginx/sites-available/auracore-api 2>/dev/null
nginx -T 2>&1 | grep -iE "conflicting|warn" | head -10
```

Expected findings: `auracore-api.bak` file present in `sites-enabled/` (B3); `auracore-api` has 3 HSTS directive lines (B4).

Record all output in the transcript doc as it runs.

- [ ] **Step 2: B3 — retire auracore-api.bak**

```bash
# Backup before moving (belt + suspenders)
cp /etc/nginx/sites-enabled/auracore-api.bak /etc/nginx/sites-enabled/auracore-api.bak.before-retire-20260417
mv /etc/nginx/sites-enabled/auracore-api.bak /etc/nginx/sites-available/archive-20260417-api.bak
nginx -t  # must say "test is successful"
```

If `nginx -t` is OK, graceful reload:

```bash
systemctl reload nginx
```

Then re-verify:

```bash
nginx -T 2>&1 | grep -iE "conflicting|warn" | head -10
```

Expected: no "conflicting server name" warnings anymore.

- [ ] **Step 3: B4 — consolidate duplicate HSTS headers**

```bash
# Find the 3 locations:
grep -n "Strict-Transport-Security" /etc/nginx/sites-available/auracore-api
```

Decision: keep exactly ONE `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;` directive at the server-block top (or in a named `http` include, matching whatever the existing single-occurrence sibling sites do — consult the `auracore.pro` site conf for the canonical placement).

```bash
# Backup before editing:
cp /etc/nginx/sites-available/auracore-api /etc/nginx/sites-available/auracore-api.bak.phase5-debt
# Edit:
vim /etc/nginx/sites-available/auracore-api
# Delete 2 of the 3 duplicate lines; keep the one at the top-level server block.
nginx -t  # must pass
systemctl reload nginx
```

Verify externally:

```bash
curl -sI https://api.auracore.pro | grep -iE "^strict-transport" | wc -l
```

Expected: `1` (single HSTS header, not 3).

- [ ] **Step 4: Write session transcript**

Create `docs/deploy/2026-04-17-nginx-debt-cleanup.md`:

```markdown
# Phase 5 Debt Sweep — Nginx Origin Cleanup (B3 + B4)

**Date:** 2026-04-17
**Host:** 165.227.170.3 (production origin)
**Operator:** <user> + Claude (assistant)
**Changes:** (1) retire stale auracore-api.bak; (2) consolidate 3 duplicate HSTS directives to 1.

## B3 — Retire auracore-api.bak

- Pre-state: `/etc/nginx/sites-enabled/auracore-api.bak` symlinked/copied, causing "conflicting server name" warnings on `nginx -T`.
- Action: `mv /etc/nginx/sites-enabled/auracore-api.bak /etc/nginx/sites-available/archive-20260417-api.bak`
- Backup: `auracore-api.bak.before-retire-20260417` preserved in `sites-enabled/` for 30 days.
- Verified: `nginx -T` no longer reports conflicting-server warnings.

## B4 — Consolidate HSTS headers

- Pre-state: `/etc/nginx/sites-available/auracore-api` had `add_header Strict-Transport-Security ...` at 3 locations.
- Action: consolidated to single directive at the server-block top (matches `auracore.pro` site pattern).
- Backup: `auracore-api.bak.phase5-debt` preserved in `sites-available/`.
- Verified externally: `curl -sI https://api.auracore.pro | grep -i strict-transport | wc -l` → `1`.

## Rollback

If any issue arises:

B3 rollback: `mv /etc/nginx/sites-available/archive-20260417-api.bak /etc/nginx/sites-enabled/auracore-api.bak && nginx -t && systemctl reload nginx`

B4 rollback: `cp /etc/nginx/sites-available/auracore-api.bak.phase5-debt /etc/nginx/sites-available/auracore-api && nginx -t && systemctl reload nginx`
```

- [ ] **Step 5: Commit the transcript**

```bash
git add docs/deploy/2026-04-17-nginx-debt-cleanup.md
git commit -m "docs(debt-B3+B4): nginx origin debt cleanup session transcript"
```

---

## Task B2: Un-skip 6 pilot-view render tests

**Files:**
- Modify: `tests/AuraCore.Tests.UI.Avalonia/Avalonia/AvaloniaTestApplication.cs` (DI bootstrap)
- Modify: `tests/AuraCore.Tests.UI.Avalonia/Responsive/PilotModulesResponsiveTests.cs` (remove `[SKIP]`)

- [ ] **Step 1: Find the test file + understand skip reason**

```bash
find tests -iname "PilotModulesResponsiveTests*" 2>/dev/null
cat tests/AuraCore.Tests.UI.Avalonia/Responsive/PilotModulesResponsiveTests.cs | head -30
```

Expected: tests marked with `Skip = "..."` attribute; reason mentions `App.Services` DI bootstrap missing.

- [ ] **Step 2: Find AvaloniaTestApplication**

```bash
find tests -iname "AvaloniaTestApplication*" -o -iname "TestApplication*" 2>/dev/null
```

Expected: a class extending `Avalonia.Application` used as the headless test harness base.

- [ ] **Step 3: Bootstrap minimal DI in AvaloniaTestApplication**

Add a static `IServiceProvider` to the test application:

```csharp
// In AvaloniaTestApplication.cs (or equivalent):
using Microsoft.Extensions.DependencyInjection;

public static IServiceProvider? TestServices { get; private set; }

public override void OnFrameworkInitializationCompleted()
{
    base.OnFrameworkInitializationCompleted();

    var services = new ServiceCollection();

    // Minimum set the 3 pilot views need:
    services.AddSingleton<AuraCore.Application.Interfaces.Platform.INavigationService>(
        NSubstitute.Substitute.For<AuraCore.Application.Interfaces.Platform.INavigationService>());
    services.AddSingleton<AuraCore.Application.Interfaces.Platform.IShellCommandService>(
        NSubstitute.Substitute.For<AuraCore.Application.Interfaces.Platform.IShellCommandService>());
    services.AddLogging();

    TestServices = services.BuildServiceProvider();

    // Point App.Services at the test container — how this integrates depends on App's
    // pattern. Typically App.Services is a static in App.axaml.cs; assign it here.
    AuraCore.UI.Avalonia.App.Services = TestServices;
}
```

If `App.Services` is get-only or private, expose a setter for test-internal use only (annotate `[InternalsVisibleTo("AuraCore.Tests.UI.Avalonia")]` on the UI project assembly to avoid public-API exposure).

- [ ] **Step 4: Un-skip the 6 tests**

```bash
grep -n "SKIP" tests/AuraCore.Tests.UI.Avalonia/Responsive/PilotModulesResponsiveTests.cs
```

Remove the `Skip = "..."` clauses from the 6 identified tests. They are:
- `BloatwareRemovalView_renders_at_wide`
- `BloatwareRemovalView_renders_at_narrow`
- `RamOptimizerView_renders_at_wide`
- `RamOptimizerView_renders_at_narrow`
- `SystemHealthView_renders_at_wide`
- `SystemHealthView_renders_at_narrow`

- [ ] **Step 5: Run the un-skipped tests**

```bash
dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --filter "FullyQualifiedName~PilotModulesResponsiveTests" --nologo 2>&1 | tail -5
```

Expected: 6 tests now running. If they fail with specific DI-resolution messages, iterate on the test-harness DI container to add the missing services (likely VMs the views instantiate).

- [ ] **Step 6: Commit**

```bash
git add tests/AuraCore.Tests.UI.Avalonia/Avalonia/AvaloniaTestApplication.cs \
        tests/AuraCore.Tests.UI.Avalonia/Responsive/PilotModulesResponsiveTests.cs
# if InternalsVisibleTo was added:
git add src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj
git commit -m "feat(debt-B2): un-skip 6 pilot-view render tests via test-harness DI bootstrap"
```

---

## Task B1: UI test flake root cause (time-boxed)

**Files:** discovery-driven; may touch `tests/AuraCore.Tests.UI.Avalonia/Avalonia/AvaloniaTestApplication.cs` + one or more test base classes, OR end up as a documentation-only task.

**Time-box: one focused investigation pass.** If root cause + fix is not achieved, document findings and escalate as Phase 6+ follow-up. Do NOT let this task block the sub-wave milestone.

- [ ] **Step 1: Collect 3 consecutive runs + diff failing test names**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
for i in 1 2 3; do
  echo "=== run $i ==="
  dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity normal --nologo 2>&1 \
    | grep -E "^  Failed " | head -10
done
```

Note which tests (if any) flake across runs. If always the same tests fail, it's likely a deterministic bug with a shared setup issue. If different tests each run, it's likely shared-static-state corruption.

- [ ] **Step 2: Hypothesis A — LocalizationService shared state**

Check: does `LocalizationService.CurrentLanguage` get reset between tests?

```bash
grep -rn "CurrentLanguage\s*=" tests/AuraCore.Tests.UI.Avalonia/ --include="*.cs" | head -10
```

If some tests set TR locale and don't reset to EN in teardown, downstream tests that assert English strings will flake. Fix: add a `TestFixture` base class or `[Collection]` with shared-state reset in `Dispose()`.

- [ ] **Step 3: Hypothesis B — Dispatcher.UIThread.Post uncompleted work**

Tests that call code invoking `Dispatcher.UIThread.Post(...)` may assert BEFORE the posted action runs. In Avalonia headless, this can be non-deterministic.

Check: grep for tests that have assertions immediately after button-click or VM-setter calls that internally Post to UIThread:

```bash
grep -rn "Dispatcher.UIThread" src/UI/AuraCore.UI.Avalonia/ --include="*.cs" | head -10
```

Mitigation: add `await Dispatcher.UIThread.InvokeAsync(() => { });` at test end to drain pending work.

- [ ] **Step 4: Hypothesis C — xUnit test parallelism**

xUnit runs tests in parallel by default; two tests mutating Avalonia's static state can race.

Check: does `tests/AuraCore.Tests.UI.Avalonia/Properties/AssemblyInfo.cs` (or a similar) set `[assembly: CollectionBehavior(DisableTestParallelization = true)]`?

```bash
find tests/AuraCore.Tests.UI.Avalonia -iname "AssemblyInfo.cs" -o -iname "*.asm*" 2>/dev/null
grep -rn "CollectionBehavior\|DisableTestParallelization" tests/AuraCore.Tests.UI.Avalonia/ --include="*.cs"
```

Fix: add `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to the test project's AssemblyInfo. This slows the run slightly but eliminates parallel state corruption — common fix for Avalonia-headless test suites.

- [ ] **Step 5: Pick the most-likely fix + apply**

Based on findings, apply ONE fix (prefer Hypothesis C — parallelism disable — as it's a single-line, low-risk, high-value change).

- [ ] **Step 6: Verify across 3 consecutive runs**

```bash
for i in 1 2 3; do
  echo "=== run $i ==="
  dotnet test tests/AuraCore.Tests.UI.Avalonia/AuraCore.Tests.UI.Avalonia.csproj --verbosity minimal --nologo 2>&1 \
    | grep -E "^Passed!|^Failed!"
done
```

Expected: all 3 runs show 0 failures.

- [ ] **Step 7a: Commit if fixed**

```bash
git add <touched files>
git commit -m "fix(debt-B1): UI test flake root cause + mitigation"
```

- [ ] **Step 7b: If not fixed within time-box — document findings + escalate**

Create `docs/debt/2026-04-17-ui-test-flake-investigation.md` with:
- Runs collected + failing test name distribution
- Hypotheses ruled in / out
- Mitigations tried
- Suggested next step (e.g., "add retry flag to CI runner", "deeper investigation into Avalonia internals — candidate for dedicated Phase 6+ task")

```bash
git add docs/debt/2026-04-17-ui-test-flake-investigation.md
git commit -m "docs(debt-B1): UI test flake investigation — time-box exhausted, escalated"
```

Then move on — don't let this task block the sub-wave milestone.

---

## Task B-milestone: Sub-wave B close

- [ ] **Step 1: Full solution test (expected green, possible retry for flake if B1 didn't land a fix)**

```bash
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!|^Failed!"
```

- [ ] **Step 2: Ceremonial commit**

```bash
git commit --allow-empty -m "milestone: Phase 5 debt sweep sub-wave B (stability + hygiene + Linux) COMPLETE"
```

---

# Phase 5 debt sweep ceremonial close

## Task Close: Memory file + MEMORY.md + ceremonial commit

**Files:**
- Create: `C:/Users/Admin/.claude/projects/C--/memory/project_ui_rebuild_phase_5_debt_sweep_complete.md`
- Modify: `C:/Users/Admin/.claude/projects/C--/memory/MEMORY.md`

- [ ] **Step 1: Write the memory file**

Template (fill in actual commit SHAs from `git log --oneline`):

```markdown
---
name: AuraCorePro UI Rebuild — Phase 5 Debt Sweep COMPLETE
description: 8 high-impact Phase 5 carry-forward debt items closed across 2 sub-waves. ChatSection ReloadAsync UI wiring + Windows privileged-helper first-run install UX + DiskHealth live WMI SMART temperature wiring + UI test flake root cause + 6 skipped pilot-view render tests un-skipped + Nginx origin ops tidy + GrubManager 3 deferred sudo hits whitelist additions.
type: project
originSessionId: 2026-04-17-phase5-debt-sweep-session
---

(full body — mirror the structure of project_ui_rebuild_phase_5_5_qa_features_complete.md: commit table, key architectural additions, test count progression, execution methodology notes, carry-forward)
```

Match the structure + tone of `project_ui_rebuild_phase_5_5_qa_features_complete.md` (one directory up in this same memory folder — use it as reference).

- [ ] **Step 2: Update MEMORY.md index**

Edit `C:/Users/Admin/.claude/projects/C--/memory/MEMORY.md`. Demote the Phase 5 close entry from CURRENT STATE to "Superseded by debt sweep"; insert new entry:

```
- [UI Rebuild Phase 5 CLOSE (Wave 3 CLOSED)](project_ui_rebuild_phase_5_close.md) — Superseded by debt sweep for current state; authoritative for Phase 5 Wave 3 umbrella. Branch merged to main at `cf5330c`. 2141 tests.
- [UI Rebuild Phase 5 Debt Sweep COMPLETE](project_ui_rebuild_phase_5_debt_sweep_complete.md) — **CURRENT STATE.** 8 items retired across 2 sub-waves (user-visible: ChatSection ReloadAsync UI + privileged-helper install UX + DiskHealth WMI SMART temp; stability: UI test flake + 6 un-skipped pilot-view tests + Nginx ops tidy + GrubManager Linux whitelist). Branch `phase-5-debt-sweep` HEAD `<ceremonial>`. <N> tests passing + 6 skipped. Merge-to-main decision pending user.
```

- [ ] **Step 3: Ceremonial empty commit**

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git commit --allow-empty -m "milestone: Phase 5 Debt Sweep COMPLETE"
git rev-parse --short HEAD
```

- [ ] **Step 4: Backfill `<ceremonial>` in the memory files**

Replace `<ceremonial>` with the actual SHA from Step 3 in both:
- `project_ui_rebuild_phase_5_debt_sweep_complete.md`
- `MEMORY.md`

---

# Plan summary

- **Tasks:** 11 (A1, A2, A3 + A milestone + B5, B3+B4, B2, B1 + B milestone + phase-close memory + ceremonial commit)
- **Estimated effort:** 2-3 days
- **Expected test growth:** 2141 → ~2170-2175
- **New NuGet:** `System.Management` 8.0.0 for A3 WMI (Microsoft BCL-adjacent, low-risk)
- **No source-code changes on non-Windows platforms** — all A work is Windows-path; B5 is Linux-only module + whitelist + polkit
- **Live ops:** 1 SSH session on origin 165.227.170.3 for B3+B4
- **Branch:** `phase-5-debt-sweep` from `main` `cf5330c`; same `--no-ff` merge pattern as Wave 3 when ready

## Self-review

**Spec coverage:**
- Spec §2 Sub-wave A items 1,2,3 → Tasks A1, A2, A3 ✓
- Spec §2 Sub-wave B items 1-5 → Tasks B5, B3+B4, B2, B1 ✓
- Spec §4 Q1 (new branch from main) → confirmed in Baseline + commit discipline ✓
- Spec §4 Q3 (B1 time-boxed) → explicit in Task B1 Step 7a/b ✓
- Spec §6 success criteria → all covered across task set ✓

**Placeholder scan:** Clean. `<ceremonial>` and `<N>` in Task Close Step 2 are intentional post-commit backfill markers (Task Close Step 4 explicitly backfills them). `TODO(phase-5.2.1)` in Task B5 Step 1 is a real source-code marker to grep for, not a plan gap.

**Type consistency:**
- `ChatSectionReloadStatus` + `ChatSectionReloadResult` + `ApplyModelChangeAsync` appear consistently across Task A1 steps 2, 4, 5.
- `IPipeProbe` + `NamedPipeProbe` + `PrivilegedHelperInstaller` + `PrivilegedHelperInstallOutcome` appear consistently across Task A2 steps 2, 3, 4, 5, 8, 9.
- `ISmartProbe` + `SmartSample` + `PickWorstTempCelsius` + `FormatWorstTemp` + `ScanCore` appear consistently across Task A3 steps 2, 4, 5.
- `GrubMkconfigValidator` + `BackupEtcGrubValidator` + `IArgvValidator` (inferred from Phase 5.5 Symlink precedent) consistent in Task B5.

Types verified; no drift found.

---

**After Phase 5 Debt Sweep:** Phase 6 brainstorm per user plan.
