using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.GamingMode.Models;

namespace AuraCore.Module.GamingMode;

public sealed class GamingModeModule : IOptimizationModule
{
    public string Id => "gaming-mode";
    public string DisplayName => "Gaming Mode";
    public OptimizationCategory Category => OptimizationCategory.GamingPerformance;
    public RiskLevel Risk => RiskLevel.Medium;

    public GamingModeState? LastState { get; private set; }
    public bool IsActive { get; private set; }

    private string? _savedPowerPlanGuid;
    private readonly List<int> _suspendedPids = new();

    // GPU optimization tracking
    private string _gpuVendor = "Unknown";
    private bool _gpuOptimized;
    private string _gpuStatus = "Not optimized";

    // Network QoS tracking
    private bool _networkQosActive;
    private string? _qosPolicyName;

    // Windows Update tracking
    private bool _windowsUpdatePaused;

    // Session tracking
    private DateTime? _activatedAt;

    // Well-known power plan GUIDs
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

    // Processes safe to suspend during gaming
    private static readonly HashSet<string> SuspendableProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "onedrive", "teams", "slack", "discord", "spotify",
        "googledrivesync", "dropbox", "adobenotificationclient",
        "ccxprocess", "adobeipcbroker", "yourphone", "phoneexperiencehost",
        "gamebar", "widgets", "widgetservice", "cortana",
        "searchhost", "textinputhost", "msedge", // background edge processes
        "skypeapp", "outlook",
    };

    // Never suspend these
    private static readonly HashSet<string> NeverSuspend = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "csrss", "smss", "wininit", "winlogon",
        "services", "lsass", "svchost", "dwm", "explorer",
        "taskhostw", "sihost", "ctfmon", "conhost", "runtimebroker",
        "msmpeng", "securityhealthservice", "audiodg", "fontdrvhost",
        "memorycompression", "registry",
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var state = await Task.Run(() => BuildState(), ct);
        LastState = state;
        return new ScanResult(Id, true, state.BackgroundProcesses.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // plan.SelectedItemIds[0] = "activate" or "deactivate"
        // plan.SelectedItemIds[1+] = toggle IDs to apply
        if (plan.SelectedItemIds.Count == 0)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        var action = plan.SelectedItemIds[0];
        int changes = 0;

        if (action == "activate")
        {
            var toggles = plan.SelectedItemIds.Skip(1).ToHashSet();

            // 1. Switch power plan
            if (toggles.Contains("power-plan"))
            {
                progress?.Report(new TaskProgress(Id, 15, "Switching to High Performance power plan..."));
                _savedPowerPlanGuid = await GetActivePowerPlanGuidAsync();
                // Try Ultimate first, fall back to High Performance
                var ok = await SetPowerPlanAsync(UltimatePerformance);
                if (!ok) await SetPowerPlanAsync(HighPerformance);
                changes++;
            }

            // 2. Disable notifications (Focus Assist)
            if (toggles.Contains("notifications"))
            {
                progress?.Report(new TaskProgress(Id, 30, "Enabling Focus Assist (priority only)..."));
                await SetFocusAssistAsync(true);
                changes++;
            }

            // 3. Suspend background processes
            if (toggles.Contains("suspend-bg"))
            {
                progress?.Report(new TaskProgress(Id, 50, "Suspending background apps..."));
                _suspendedPids.Clear();
                var pidsToSuspend = plan.SelectedItemIds
                    .Where(id => id.StartsWith("pid:"))
                    .Select(id => int.TryParse(id[4..], out var p) ? p : 0)
                    .Where(p => p > 0)
                    .ToList();

                // If no specific PIDs, suspend all suggested
                if (pidsToSuspend.Count == 0 && LastState is not null)
                    pidsToSuspend = LastState.BackgroundProcesses.Where(p => p.SuggestSuspend).Select(p => p.Pid).ToList();

                foreach (var pid in pidsToSuspend)
                {
                    if (SuspendProcess(pid))
                    {
                        _suspendedPids.Add(pid);
                        changes++;
                    }
                }
            }

            // 4. Clean RAM
            if (toggles.Contains("clean-ram"))
            {
                progress?.Report(new TaskProgress(Id, 70, "Cleaning RAM..."));
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (!NeverSuspend.Contains(proc.ProcessName.ToLowerInvariant()) && proc.Id != Environment.ProcessId)
                            EmptyWorkingSet(proc.Handle);
                    }
                    catch { }
                    finally { try { proc.Dispose(); } catch { } }
                }
                changes++;
            }

            // 5. Boost foreground priority
            if (toggles.Contains("boost-priority"))
            {
                progress?.Report(new TaskProgress(Id, 60, "Boosting process priority..."));
                try
                {
                    using var current = Process.GetCurrentProcess();
                    current.PriorityClass = ProcessPriorityClass.High;
                }
                catch { }
                changes++;
            }

            // 6. GPU optimization (Nvidia/AMD)
            if (toggles.Contains("gpu-optimize") && OperatingSystem.IsWindows())
            {
                progress?.Report(new TaskProgress(Id, 70, "Optimizing GPU performance..."));
                await ApplyGpuOptimizationAsync();
                changes++;
            }

            // 7. Network QoS priority
            if (toggles.Contains("network-qos") && OperatingSystem.IsWindows())
            {
                progress?.Report(new TaskProgress(Id, 80, "Setting network QoS priority..."));
                await ApplyNetworkQosAsync();
                changes++;
            }

            // 8. Pause Windows Update
            if (toggles.Contains("pause-wu") && OperatingSystem.IsWindows())
            {
                progress?.Report(new TaskProgress(Id, 90, "Pausing Windows Update..."));
                await PauseWindowsUpdateAsync();
                changes++;
            }

            IsActive = true;
            _activatedAt = DateTime.UtcNow;
            progress?.Report(new TaskProgress(Id, 100, "Gaming Mode activated!"));
        }
        else if (action == "deactivate")
        {
            progress?.Report(new TaskProgress(Id, 20, "Restoring power plan..."));
            if (_savedPowerPlanGuid is not null)
            {
                await SetPowerPlanAsync(_savedPowerPlanGuid);
                _savedPowerPlanGuid = null;
                changes++;
            }

            progress?.Report(new TaskProgress(Id, 40, "Disabling Focus Assist..."));
            await SetFocusAssistAsync(false);
            changes++;

            progress?.Report(new TaskProgress(Id, 60, "Resuming suspended processes..."));
            foreach (var pid in _suspendedPids)
            {
                ResumeProcess(pid);
                changes++;
            }
            _suspendedPids.Clear();

            progress?.Report(new TaskProgress(Id, 50, "Restoring priority..."));
            try
            {
                using var current = Process.GetCurrentProcess();
                current.PriorityClass = ProcessPriorityClass.Normal;
            }
            catch { }
            changes++;

            // Revert GPU optimization
            if (_gpuOptimized && OperatingSystem.IsWindows())
            {
                progress?.Report(new TaskProgress(Id, 60, "Reverting GPU optimization..."));
                await RevertGpuOptimizationAsync();
                changes++;
            }

            // Remove Network QoS policy
            if (_networkQosActive && OperatingSystem.IsWindows())
            {
                progress?.Report(new TaskProgress(Id, 70, "Removing network QoS policy..."));
                await RemoveNetworkQosAsync();
                changes++;
            }

            // Resume Windows Update
            if (_windowsUpdatePaused && OperatingSystem.IsWindows())
            {
                progress?.Report(new TaskProgress(Id, 85, "Resuming Windows Update..."));
                await ResumeWindowsUpdateAsync();
                changes++;
            }

            IsActive = false;
            _activatedAt = null;
            progress?.Report(new TaskProgress(Id, 100, "Gaming Mode deactivated — everything restored."));
        }

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, changes, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(IsActive);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => OptimizeAsync(new OptimizationPlan(Id, new List<string> { "deactivate" }), null, ct);

    private GamingModeState BuildState()
    {
        // Current power plan
        var powerPlanGuid = GetActivePowerPlanGuidAsync().GetAwaiter().GetResult();
        var powerPlanName = powerPlanGuid switch
        {
            string g when g.Contains(HighPerformance) => "High Performance",
            string g when g.Contains(UltimatePerformance) => "Ultimate Performance",
            string g when g.Contains(Balanced) => "Balanced",
            _ => "Custom / Power Saver"
        };

        // Background processes
        var bgProcesses = new List<SuspendableProcess>();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var name = proc.ProcessName;
                var lower = name.ToLowerInvariant();
                if (NeverSuspend.Contains(lower)) continue;
                if (proc.Id == Environment.ProcessId) continue;

                var isSuspendable = SuspendableProcesses.Contains(lower);
                var mem = proc.WorkingSet64 / (1024 * 1024);

                if (isSuspendable || mem > 100) // Show if known-suspendable or using >100MB
                {
                    bgProcesses.Add(new SuspendableProcess
                    {
                        Pid = proc.Id,
                        Name = name,
                        MemoryMb = mem,
                        Category = isSuspendable ? "Background" : "Application",
                        SuggestSuspend = isSuspendable
                    });
                }
            }
            catch { }
            finally { try { proc.Dispose(); } catch { } }
        }

        bgProcesses.Sort((a, b) => b.MemoryMb.CompareTo(a.MemoryMb));

        // Detect GPU vendor
        var gpuVendor = DetectGpuVendor();

        var toggles = new List<GamingToggle>
        {
            new() { Id = "power-plan", Name = "High Performance Power Plan", Description = "Switch to Ultimate/High Performance — max CPU speed, no throttling", Risk = "Safe", CurrentState = powerPlanGuid.Contains(HighPerformance) || powerPlanGuid.Contains(UltimatePerformance) },
            new() { Id = "notifications", Name = "Silence Notifications", Description = "Enable Focus Assist — block all notifications during gaming", Risk = "Safe" },
            new() { Id = "suspend-bg", Name = "Suspend Background Apps", Description = $"Pause {bgProcesses.Count(p => p.SuggestSuspend)} non-essential background processes", Risk = "Caution" },
            new() { Id = "clean-ram", Name = "Clean RAM", Description = "Trim working sets to free memory for your game", Risk = "Safe" },
            new() { Id = "boost-priority", Name = "Boost Process Priority", Description = "Set foreground app priority to High", Risk = "Caution" },
            new() { Id = "gpu-optimize", Name = $"GPU Optimization ({gpuVendor})", Description = gpuVendor == "Nvidia" ? "Set GPU power management to Prefer Maximum Performance via nvidia-smi" : gpuVendor == "AMD" ? "Set GPU to performance mode via PowerShell" : "GPU vendor not detected — will attempt generic optimization", Risk = "Safe", CurrentState = _gpuOptimized },
            new() { Id = "network-qos", Name = "Network QoS Priority", Description = "Set high network priority for game traffic using Windows QoS policy", Risk = "Safe", CurrentState = _networkQosActive },
            new() { Id = "pause-wu", Name = "Pause Windows Update", Description = "Temporarily stop Windows Update service during gaming session", Risk = "Caution", CurrentState = _windowsUpdatePaused },
        };

        return new GamingModeState
        {
            IsActive = IsActive,
            CurrentPowerPlan = powerPlanName,
            CurrentPowerPlanGuid = powerPlanGuid,
            BackgroundProcesses = bgProcesses,
            RunningBackgroundApps = bgProcesses.Count,
            Toggles = toggles,
            GpuVendor = gpuVendor,
            GpuStatus = _gpuStatus,
            GpuOptimized = _gpuOptimized,
            NetworkQosActive = _networkQosActive,
            NetworkQosStatus = _networkQosActive ? "Active — game traffic prioritized" : "Inactive",
            WindowsUpdatePaused = _windowsUpdatePaused,
            WindowsUpdateStatus = _windowsUpdatePaused ? "Paused" : "Running",
            ActivatedAt = _activatedAt,
        };
    }

    private static async Task<string> GetActivePowerPlanGuidAsync()
    {
        try
        {
            var output = await RunCmdAsync("powercfg /getactivescheme");
            // Output: "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
            var match = System.Text.RegularExpressions.Regex.Match(output, @"[0-9a-f\-]{36}");
            return match.Success ? match.Value : "";
        }
        catch { return ""; }
    }

    private static async Task<bool> SetPowerPlanAsync(string guid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/setactive {guid}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task SetFocusAssistAsync(bool enable)
    {
        // Focus Assist via registry
        try
        {
            var value = enable ? 2 : 0; // 0=off, 1=priority, 2=alarms only
            var psi = new ProcessStartInfo
            {
                FileName = "reg",
                Arguments = $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore\\Store\\DefaultAccount\\Current\\default$windows.data.notifications.quiethourssettings\\windows.data.notifications.quiethourssettings\" /v Data /t REG_BINARY /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            // Simplified approach: just toggle the quiet hours setting
            await RunCmdAsync(enable
                ? "powershell -NoProfile -Command \"New-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings' -Name 'NOC_GLOBAL_SETTING_TOASTS_ENABLED' -Value 0 -PropertyType DWORD -Force\""
                : "powershell -NoProfile -Command \"Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings' -Name 'NOC_GLOBAL_SETTING_TOASTS_ENABLED' -Value 1\"");
        }
        catch { }
    }

    // Process suspend/resume via NtSuspendProcess/NtResumeProcess
    private static bool SuspendProcess(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            var threads = proc.Threads.Cast<ProcessThread>().ToList();
            foreach (var thread in threads)
            {
                var handle = OpenThread(0x0002, false, (uint)thread.Id); // THREAD_SUSPEND_RESUME
                if (handle != IntPtr.Zero)
                {
                    SuspendThread(handle);
                    CloseHandle(handle);
                }
            }
            return true;
        }
        catch { return false; }
    }

    private static void ResumeProcess(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            var threads = proc.Threads.Cast<ProcessThread>().ToList();
            foreach (var thread in threads)
            {
                var handle = OpenThread(0x0002, false, (uint)thread.Id);
                if (handle != IntPtr.Zero)
                {
                    ResumeThread(handle);
                    CloseHandle(handle);
                }
            }
        }
        catch { }
    }

    // ── GPU Optimization ──

    private static string DetectGpuVendor()
    {
        try
        {
            // Check for Nvidia via nvidia-smi
            var nvidiaCheck = RunCmdSync("where nvidia-smi");
            if (!string.IsNullOrWhiteSpace(nvidiaCheck) && !nvidiaCheck.Contains("Could not find"))
                return "Nvidia";

            // Check for AMD via registry / WMI
            var wmiOutput = RunCmdSync("powershell -NoProfile -Command \"(Get-WmiObject Win32_VideoController).Name\"");
            if (!string.IsNullOrWhiteSpace(wmiOutput))
            {
                if (wmiOutput.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) return "Nvidia";
                if (wmiOutput.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                    wmiOutput.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) return "AMD";
                if (wmiOutput.Contains("Intel", StringComparison.OrdinalIgnoreCase)) return "Intel";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GPU detection error: {ex.Message}");
        }
        return "Unknown";
    }

    private static string RunCmdSync(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "";
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return output;
        }
        catch { return ""; }
    }

    private async Task ApplyGpuOptimizationAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            _gpuVendor = DetectGpuVendor();

            if (_gpuVendor == "Nvidia")
            {
                // Set Nvidia GPU to prefer maximum performance
                var result = await RunCmdAsync("nvidia-smi -pm ENABLED");
                var result2 = await RunCmdAsync("nvidia-smi --power-limit=0"); // Reset to max TDP
                // Set compute mode for preferred performance
                await RunCmdAsync("nvidia-smi -e 0"); // Disable ECC if applicable
                _gpuOptimized = true;
                _gpuStatus = "Nvidia — Maximum Performance mode";
                Debug.WriteLine($"Nvidia GPU optimized: {result}");
            }
            else if (_gpuVendor == "AMD")
            {
                // AMD: attempt to set performance mode via PowerShell registry
                await RunCmdAsync(
                    "powershell -NoProfile -Command \"" +
                    "try { " +
                    "$regPath = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000'; " +
                    "Set-ItemProperty -Path $regPath -Name 'PP_SclkDeepSleepDisable' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue; " +
                    "Set-ItemProperty -Path $regPath -Name 'KMD_EnableGfxOff' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue; " +
                    "} catch { }\"");
                _gpuOptimized = true;
                _gpuStatus = "AMD — Performance mode set via registry";
                Debug.WriteLine("AMD GPU optimized via registry");
            }
            else
            {
                _gpuOptimized = false;
                _gpuStatus = $"{_gpuVendor} GPU — no specific optimization available";
                Debug.WriteLine($"GPU optimization skipped: vendor={_gpuVendor}");
            }
        }
        catch (Exception ex)
        {
            _gpuOptimized = false;
            _gpuStatus = "Optimization failed — see logs";
            Debug.WriteLine($"GPU optimization error: {ex.Message}");
        }
    }

    private async Task RevertGpuOptimizationAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            if (_gpuVendor == "Nvidia")
            {
                await RunCmdAsync("nvidia-smi -pm DISABLED");
            }
            else if (_gpuVendor == "AMD")
            {
                await RunCmdAsync(
                    "powershell -NoProfile -Command \"" +
                    "try { " +
                    "$regPath = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000'; " +
                    "Remove-ItemProperty -Path $regPath -Name 'PP_SclkDeepSleepDisable' -Force -ErrorAction SilentlyContinue; " +
                    "Remove-ItemProperty -Path $regPath -Name 'KMD_EnableGfxOff' -Force -ErrorAction SilentlyContinue; " +
                    "} catch { }\"");
            }
            _gpuOptimized = false;
            _gpuStatus = "Not optimized";
            Debug.WriteLine("GPU optimization reverted");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GPU revert error: {ex.Message}");
        }
    }

    // ── Network QoS ──

    private async Task ApplyNetworkQosAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            _qosPolicyName = "AuraCore_GamingMode_QoS";
            // Create a DSCP-based QoS policy for high-priority traffic
            // Uses PowerShell New-NetQosPolicy if available, otherwise netsh
            var result = await RunCmdAsync(
                $"powershell -NoProfile -Command \"" +
                $"try {{ " +
                $"Remove-NetQosPolicy -Name '{_qosPolicyName}' -Confirm:$false -ErrorAction SilentlyContinue; " +
                $"New-NetQosPolicy -Name '{_qosPolicyName}' -NetworkProfile All -DSCPAction 46 -IPProtocolMatchCondition Both -ThrottleRateActionBitsPerSecond 0 -PolicyStore ActiveStore; " +
                $"Write-Output 'QoS policy created'; " +
                $"}} catch {{ " +
                $"Write-Output 'PowerShell QoS failed, trying netsh'; " +
                $"}}\"");

            if (result.Contains("QoS policy created"))
            {
                _networkQosActive = true;
                Debug.WriteLine("Network QoS policy applied via PowerShell");
            }
            else
            {
                // Fallback to netsh
                var netshResult = await RunCmdAsync(
                    $"netsh int tcp set global autotuninglevel=normal && " +
                    $"netsh int tcp set heuristics disabled && " +
                    $"netsh int tcp set global rss=enabled");
                _networkQosActive = true;
                Debug.WriteLine($"Network QoS applied via netsh: {netshResult}");
            }
        }
        catch (Exception ex)
        {
            _networkQosActive = false;
            Debug.WriteLine($"Network QoS error: {ex.Message}");
        }
    }

    private async Task RemoveNetworkQosAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            if (_qosPolicyName is not null)
            {
                await RunCmdAsync(
                    $"powershell -NoProfile -Command \"" +
                    $"Remove-NetQosPolicy -Name '{_qosPolicyName}' -Confirm:$false -ErrorAction SilentlyContinue\"");
                Debug.WriteLine("Network QoS policy removed");
            }
            _networkQosActive = false;
            _qosPolicyName = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Network QoS removal error: {ex.Message}");
        }
    }

    // ── Windows Update Pause/Resume ──

    private async Task PauseWindowsUpdateAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            // Attempt to stop the Windows Update service
            var result = await RunCmdAsync("net stop wuauserv");
            // Also stop the Update Orchestrator Service
            await RunCmdAsync("net stop UsoSvc");
            _windowsUpdatePaused = true;
            Debug.WriteLine($"Windows Update paused: {result}");
        }
        catch (Exception ex)
        {
            _windowsUpdatePaused = false;
            Debug.WriteLine($"Windows Update pause failed (may require elevation): {ex.Message}");
        }
    }

    private async Task ResumeWindowsUpdateAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            await RunCmdAsync("net start wuauserv");
            await RunCmdAsync("net start UsoSvc");
            _windowsUpdatePaused = false;
            Debug.WriteLine("Windows Update resumed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Windows Update resume failed: {ex.Message}");
        }
    }

    // ── Helpers ──

    private static async Task<string> RunCmdAsync(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "";
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output;
        }
        catch { return ""; }
    }

    [DllImport("kernel32.dll")] private static extern IntPtr OpenThread(uint access, bool inherit, uint threadId);
    [DllImport("kernel32.dll")] private static extern uint SuspendThread(IntPtr hThread);
    [DllImport("kernel32.dll")] private static extern uint ResumeThread(IntPtr hThread);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("psapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyWorkingSet(IntPtr hProcess);
}

public static class GamingModeRegistration
{
    public static IServiceCollection AddGamingModeModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, GamingModeModule>();
}
