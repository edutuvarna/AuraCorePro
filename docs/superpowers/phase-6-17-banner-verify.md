# Phase 6.17 PrivilegeHelperMissingBanner Linux Verify

**VM:** Ubuntu 24.04.4 LTS / VMware (192.168.162.129)
**Build:** `phase-6-17-debt-closure` HEAD <set during Wave H ship>
**Date launched:** <YYYY-MM-DD when user runs the GUI smoke>

## Pre-fix state (Phase 6.16 ship)

- `IHelperAvailabilityService.IsBannerVisible` defaulted to `false`
- Only flipped `true` via `ReportMissing()` call
- `ReportMissing()` only invoked after a privileged op fails
- Result: banner never showed at startup on Linux even when helper missing — user only saw it after attempting RAM Optimizer / similar AND that attempt failed loudly enough to trigger ReportMissing()

## Fix applied (commit <Wave G commit SHA, fill in>)

`src/Desktop/AuraCore.Desktop.Services/PrivilegeIpc/HelperAvailabilityService.cs`:
- Added ctor-level startup probe (Linux + macOS only; Windows uses UAC, banner not relevant)
- Probe is fire-and-forget `Task.Run` — banner surfaces within ~milliseconds of app launch
- Source of truth: `/opt/auracorepro/install-privhelper.sh.installed` file-existence sentinel
- The sentinel is written by `install-privhelper.sh` upon successful daemon registration
- Real D-Bus presence probe is Phase 6.18 (requires `Tmds.DBus` session-bus query)

Banner XAML / code-behind / 3 loc keys (EN+TR) were already in place from Phase 5.2.0 Task 11; no changes needed in 6.17.G.

## Existing banner copy (Phase 5.2.0 baseline — preserved as-is)

| Key | EN | TR |
|---|---|---|
| `privilege.missing.banner_text` | "Privilege helper is not installed. Some modules can't perform system changes." | "Ayrıcalık yardımcısı yüklü değil. Bazı modüller sistem değişikliği yapamayabilir." |
| `privilege.missing.btn_install_now` | "Install now" | "Şimdi yükle" |
| `privilege.missing.btn_dismiss` | "Dismiss" | "Kapat" |

The copy is actionable (mentions consequence + Install Now button). No update applied in 6.17.G.

## Operator smoke test instructions (run when waking up)

```bash
# SSH to VM (or use VM desktop directly)
ssh -i ~/.ssh/id_ed25519_aura deniz@192.168.162.129

# Confirm the install marker is NOT present (helper not deployed in 6.17 — that's 6.18)
test -f /opt/auracorepro/install-privhelper.sh.installed && echo "MARKER PRESENT (banner should hide)" || echo "MARKER ABSENT (banner should show)"

# Launch the new build (delivered via Wave H scp)
cd ~/auracore-pro-6.17-test && ./AuraCore.Pro
```

Then on the VM desktop:
1. Login
2. Look at the top of MainWindow

## Smoke verification matrix (operator fills in)

| Check | Expected | Actual |
|---|---|---|
| Banner visible immediately after MainWindow shows? | yes (helper marker absent) | <yes/no> |
| Banner text reads "Privilege helper is not installed..." | yes | <yes/no> |
| "Install now" button visible | yes | <yes/no> |
| "Dismiss" button visible | yes | <yes/no> |
| Clicking Dismiss hides the banner for the session | yes (existing 5.2.0 behavior) | <yes/no> |
| Clicking RAM Optimizer → "Optimize Now" → see PrivilegeHelperRequiredDialog (Wave E modal) | yes | <yes/no> |
| Cancelling that dialog returns to RAM Optimizer view with post-action banner showing "Skipped: Privilege helper required" + remediation | yes | <yes/no> |

## Pass / fail criteria

- ✅ **PASS** — banner shows at startup; modal shows on privileged action click; post-action banner shows "Skipped" feedback.
- ❌ **FAIL** — banner doesn't show OR modal doesn't show OR success feedback shown despite helper missing.

If FAIL: re-cycle 6.17.G fix (likely the file-existence probe path is wrong on the user's distro, or the event wiring in `MainWindow.SyncBannerVisibility` is broken).

## Sign-off

Operator: __________________________  Date: ___________
Screenshot of banner: __________________________ (attach or path)

## Carry-forward → Phase 6.18

- Replace file-existence sentinel with real D-Bus presence probe (Tmds.DBus session-bus listing for `org.auracore.PrivHelper` service name)
- Auto-refresh: subscribe to D-Bus `NameOwnerChanged` so banner hides immediately after `install-privhelper.sh` registers the daemon (no app restart needed)
- Wire the `InstallNowClicked` event handler to actually launch the install script via `pkexec` / sudo prompt
- Verify the "Install now" button currently in MainWindow (should bring up a separate flow that's not part of 6.17.G's scope)
