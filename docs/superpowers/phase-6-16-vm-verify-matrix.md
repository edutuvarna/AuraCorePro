# Phase 6.16 Linux VM Smoke Test Matrix

**VM:** Ubuntu 24.04.4 LTS / VMware
**Host:** 192.168.162.129
**Build:** `phase-6-16-linux-platform-awareness` HEAD `cef7b1f256ae100e95f86bdbf597dda1d8b41645`
**Build artifact:** `~/auracore-pro-6.16-test/` (extracted from `~/auracore-pro-6.16.tar.gz`)
**Date launched:** <YYYY-MM-DD when user runs the GUI smoke>

## Automated probe results

- SSH connectivity: ok (`Linux deniz-VMware-Virtual-Platform 6.17.0-20-generic #20~24.04.1-Ubuntu SMP PREEMPT_DYNAMIC Thu Mar 19 01:28:37 UTC 2 x86_64 x86_64 x86_64 GNU/Linux`)
- App binary launches without immediate native crash: yes — Avalonia AppBuilder ran successfully through `Setup()`/`SetupWithLifetime()`, native deps loaded, X11 backend initialized; only failed on `XOpenDisplay` (no display server available over SSH session — expected). No `EntryPointNotFoundException`, no `PlatformNotSupportedException`, no `SIGSEGV`. Exception is a clean managed `System.Exception` thrown from `AvaloniaX11Platform.Initialize`.
- Output during 3-second timeout probe (last 30 lines):
  ```
  === Launching with timeout 3s, no DISPLAY (headless probe) ===
  Unhandled exception. System.Exception: XOpenDisplay failed
     at Avalonia.X11.AvaloniaX11Platform.Initialize(X11PlatformOptions options)
     at Avalonia.AvaloniaX11PlatformExtensions.<>c.<UseX11>b__0_0()
     at Avalonia.AppBuilder.SetupUnsafe()
     at Avalonia.AppBuilder.Setup()
     at Avalonia.AppBuilder.SetupWithLifetime(IApplicationLifetime lifetime)
     at Avalonia.ClassicDesktopStyleApplicationLifetimeExtensions.StartWithClassicDesktopLifetime(AppBuilder builder, String[] args, Action`1 lifetimeBuilder)
     at AuraCore.UI.Avalonia.Program.Main(String[] args) in C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro\src\UI\AuraCore.UI.Avalonia\Program.cs:line 87
  timeout: the monitored command dumped core
  === Exit code: 0 ===
  ```

  Note: `timeout` reported the runtime invoking `createdump` (standard .NET behavior on unhandled managed exception — not a SIGSEGV). Exit 0 is the wrapper script's overall status; the actual app process aborted cleanly via the runtime's unhandled-exception handler.

## Per-module GUI matrix (filled in by user during VM desktop session)

Each row: open the sidebar, click the module, observe the rendered area. Mark each:
- **Sidebar visible:** yes / no (Windows-only modules should be hidden on Linux)
- **Crashed:** yes / no (any module that crashes the app blocks v1.8.0)
- **Render:** OK (real module view) / Unavailable (UnavailableModuleView with reason+remediation) / Dashboard-fallback (BUG — shouldn't happen post-Phase-6.16)

| Category | Module ID | Sidebar visible | Crashed | Render | Notes |
|----------|-----------|-----------------|---------|--------|-------|
| Always | dashboard | yes | | | |
| Always | ai-features | yes | | | |
| Always | settings | yes | | | |
| Optimize | ram-optimizer | yes | | | |
| Optimize | startup-optimizer | NO (Windows) | | | |
| Optimize | network-optimizer | NO (Windows) | | | |
| Optimize | battery-optimizer | NO (Windows) | | | |
| Optimize | storage-compression | NO (Windows) | | | |
| Optimize | systemd-manager | yes | | | Should show UnavailableModuleView IF systemctl missing |
| Optimize | swap-optimizer | yes | | | Should show UnavailableModuleView IF swapon missing |
| Clean & Debloat | junk-cleaner | yes | | | |
| Clean & Debloat | disk-cleanup | yes | | | |
| Clean & Debloat | privacy-cleaner | yes | | | |
| Clean & Debloat | registry-cleaner | NO (Windows) | | | |
| Clean & Debloat | bloatware-removal | NO (Windows) | | | |
| Clean & Debloat | package-cleaner | yes | | | |
| Clean & Debloat | journal-cleaner | yes | | | |
| Clean & Debloat | snap-flatpak-cleaner | yes | | | |
| Clean & Debloat | kernel-cleaner | yes | | | |
| Gaming | gaming-mode | NO (Windows) | | | |
| Security | defender-manager | NO (Windows) | | | |
| Security | firewall-rules | NO (Windows) | | | |
| Security | file-shredder | yes | | | |
| Security | hosts-editor | yes | | | |
| Apps & Tools | app-installer | NO (Windows) | | | |
| Apps & Tools | driver-updater | NO (Windows) | | | |
| Apps & Tools | service-manager | NO (Windows) | | | |
| Apps & Tools | iso-builder | NO (Windows) | | | |
| Apps & Tools | space-analyzer | yes | | | |
| Apps & Tools | system-health | yes | | | |
| Apps & Tools | linux-app-installer | yes | | | |
| Advanced | registry-deep | NO (Windows) | | | |
| Advanced | environment-variables | yes | | | |
| Advanced | symlink-manager | yes | | | |
| Advanced | process-monitor | yes | | | |
| Advanced | context-menu | NO (Windows) | | | |
| Advanced | taskbar-tweaks | NO (Windows) | | | |
| Advanced | explorer-tweaks | NO (Windows) | | | |
| Advanced | autorun-manager | NO (Windows) | | | |
| Advanced | wake-on-lan | yes | | | |
| Advanced | admin-panel | yes | | | |
| Advanced | cron-manager | yes | | | |
| Advanced | docker-cleaner | yes | | | |
| Advanced | grub-manager | yes | | | |

## Pass / fail criteria

- PASS — every row's Crashed column is `no`. Windows-only modules' Sidebar-visible column is `NO`. Linux+cross modules render either OK or UnavailableModuleView (never Dashboard-fallback).
- FAIL — any crash, OR any "Dashboard-fallback" render, OR any Windows module visible in sidebar.

If FAIL: file an issue against the responsible Wave (B / C / D / E / F) and re-cycle Phase 6.16 fixes; rebuild + re-deploy + retest.

## Sign-off

Operator: _________________________  Date: _________

---

## Phase 6.17 verification (added 2026-05-04)

**Build:** `phase-6-17-debt-closure` HEAD `4f2bb9f83c9e10457d4abe437130779793a0e0e3`
**Status:** Operator smoke pending (run when ready — see `docs/superpowers/phase-6-17-banner-verify.md` for banner matrix)

| Module (post-6.17 adoption) | Post-action banner shows? | Helper-missing path correct? | Notes |
|---|---|---|---|
| RAM Optimizer    | <yes/no> | <yes/no> | |
| Junk Cleaner     | <yes/no> | <yes/no> | |
| Systemd Manager  | <yes/no> | <yes/no> | |
| Swap Optimizer   | <yes/no> | <yes/no> | |
| Package Cleaner  | <yes/no> | <yes/no> | |
| Journal Cleaner  | <yes/no> | <yes/no> | |

System Health no longer shows -2147483648%: <yes/no>
PrivilegeHelperMissingBanner visible at startup (helper not installed): <yes/no>

Sign-off: __________________  Date: ____________
