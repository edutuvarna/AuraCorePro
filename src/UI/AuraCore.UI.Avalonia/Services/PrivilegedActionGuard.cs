using global::Avalonia.Controls.ApplicationLifetimes;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.UI.Avalonia.Views.Dialogs;

namespace AuraCore.UI.Avalonia.Services;

/// <summary>
/// Phase 6.17: UI-shell implementation of IPrivilegedActionGuard.
/// Windows: short-circuits to true (UAC handles elevation per-process).
/// Linux/macOS: checks IHelperAvailabilityService.IsMissing; if missing,
/// shows a modal PrivilegeHelperRequiredDialog and returns false so the
/// caller's Skipped path renders user feedback.
/// </summary>
public sealed class PrivilegedActionGuard : IPrivilegedActionGuard
{
    private readonly IHelperAvailabilityService _helper;

    public PrivilegedActionGuard(IHelperAvailabilityService helper) => _helper = helper;

    public async Task<bool> TryGuardAsync(
        string actionDescription,
        string? remediationCommandOverride = null,
        CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows()) return true;
        if (!_helper.IsMissing) return true;

        var remediation = remediationCommandOverride
            ?? "sudo bash /opt/auracorepro/install-privhelper.sh";

        // Find the active window via the desktop application lifetime — the
        // existing pattern used by App.axaml.cs's OnInstanceIntentReceived.
        var owner = (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner is not null)
        {
            var dialog = new PrivilegeHelperRequiredDialog(actionDescription, remediation);
            await dialog.ShowDialog(owner);
        }
        // If we can't find a window (test harness, headless), return false silently —
        // the caller's "Skipped" path will render UI feedback inline.
        return false;
    }
}
