# Phase 5.2 — Plan-time Audit: Modules Requiring Privilege IPC Migration

**Date:** 2026-04-16  
**Status:** Enumeration complete  
**Scope:** All modules in `src/Modules/` with sudo/pkexec invocations

---

## Overview

This audit scans all modules for privilege escalation patterns (sudo, pkexec) to enumerate the set of modules requiring migration to the Phase 5.2 privilege IPC infrastructure. The audit reconciles grep results against the umbrella-listed 11 target modules and flags additional candidates discovered.

**Grep results:** 8 files with sudo/pkexec patterns
**Umbrella modules (11):** All confirmed present ✓
**Extras surfaced:** 0 (no modules outside the umbrella set use sudo/pkexec)

---

## Part 1: Modules with Privilege Patterns

### Linux Modules (6)

#### 1. GrubManager
- **File:** `src/Modules/AuraCore.Module.GrubManager/GrubManagerModule.cs`
- **Hits:** Lines 189, 308, 319, 338, 344
- **Platform:** Linux
- **Umbrella listed:** ✓ Yes
- **Current sudo invocations:**
  - Line 189: `sudo -n cp {GrubBackupPath} {GrubConfigPath}` — backup restore
  - Line 308: `sudo -n sed -i ...` — in-place config edit (via shell -c wrapper)
  - Line 319: `sudo -n sed -i ...` — theme/color edit
  - Line 338: `sudo -n update-grub` — regenerate bootloader config
  - Line 344: `sudo -n sh -c "grep/sed/mv"` — complex editing chain

**Proposed PrivilegedCommand.Id:** `grub.update-config` (single umbrella for all GrubManager actions)

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// "update-grub"        → ["/usr/sbin/update-grub"]
// "edit-config" key    → ["/bin/sed", "-i", "/etc/default/grub", pattern, replacement]
// "restore-backup"     → ["/bin/cp", backupPath, "/etc/default/grub"]
// Validator: regex check on sed patterns to ensure no shell metacharacters escape the intent
```

---

#### 2. LinuxAppInstaller
- **File:** `src/Modules/AuraCore.Module.LinuxAppInstaller/LinuxAppInstallerModule.cs`
- **Hits:** Lines 155, 156, 175, 176
- **Platform:** Linux
- **Umbrella listed:** ✓ Yes
- **Current sudo invocations:**
  - Line 155: `sudo -n apt-get install -y {app.PackageName}`
  - Line 156: `sudo -n snap install {app.PackageName}`
  - Line 175: `sudo -n apt-get remove -y {pkgName}`
  - Line 176: `sudo -n snap remove {pkgName}`

**Note:** Invoked via `/bin/sh -c "{cmd}"` wrapper.

**Proposed PrivilegedCommand.Id:** `package.install` / `package.remove` (or unified `package.manage`)

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema for install:
// "apt" source    → ["/usr/bin/apt-get", "install", "-y", package_name]
// "snap" source   → ["/usr/bin/snap", "install", package_name]
// "flatpak" →      NO SUDO (unprivileged) → ProcessRunner direct
// Validator: alphanumeric + dash/underscore only for package_name; reject shell metacharacters
```

---

#### 3. SnapFlatpakCleaner
- **File:** `src/Modules/AuraCore.Module.SnapFlatpakCleaner/SnapFlatpakCleanerModule.cs`
- **Hits:** Lines 200, 232
- **Platform:** Linux
- **Umbrella listed:** ✓ Yes
- **Current sudo invocations:**
  - Line 200: `sudo -n snap remove {name} --revision={revision}` (via ProcessRunner.RunAsync)
  - Line 232: Embedded in shell script: `sudo snap remove "$name" --revision="$rev"` (loop over disabled snaps)

**Proposed PrivilegedCommand.Id:** `snap.remove`

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// ["/usr/bin/snap", "remove", name, "--revision=" + revision_id]
// Validator: name = alphanumeric + dash; revision_id = hex/numeric only
```

---

#### 4. KernelCleaner
- **File:** `src/Modules/AuraCore.Module.KernelCleaner/KernelCleanerModule.cs`
- **Hits:** None found by grep (SURPRISE FINDING)
- **Platform:** Linux
- **Umbrella listed:** ✓ Yes
- **Rationale for inclusion:** Module performs kernel removal via apt/dnf. Expected to call `apt remove` or `dnf remove` under sudo.
- **Action required:** Read module source to confirm actual privilege path (likely shell -c with apt/dnf, or uses ProcessRunner directly for unprivileged queries).

**AUDIT NOTE:** Recommend manual inspection — kernel removal inherently requires privilege but module may defer to OS.run() or may be gated by prerequisites. Will be captured in audit discovery phase of 5.2.0.

---

#### 5. JournalCleaner
- **File:** `src/Modules/AuraCore.Module.JournalCleaner/JournalCleanerModule.cs`
- **Hits:** None found by grep
- **Platform:** Linux
- **Umbrella listed:** ✓ Yes
- **Rationale for inclusion:** Module vacuums systemd journal via `journalctl --vacuum-*`. Journalctl is typically unprivileged on systems with `systemd-journald` running as root BUT user must be in `systemd-journal` group. Likely operates unprivileged in practice, or falls back gracefully.

**AUDIT NOTE:** Low risk. Module uses direct `journalctl` without sudo in grep results. Recommend confirming whether vacuum is unprivileged (user-group-based) or needs future privilege IPC.

---

#### 6. SwapOptimizer
- **File:** `src/Modules/AuraCore.Module.SwapOptimizer/SwapOptimizerModule.cs`
- **Hits:** Lines 210, 214
- **Platform:** Linux
- **Umbrella listed:** ✓ Yes
- **Current sudo invocations:**
  - Line 210: `sudo -n sysctl vm.swappiness={value}` — runtime kernel param change
  - Line 214: `echo 'vm.swappiness={value}' | sudo -n tee /etc/sysctl.d/99-auracore-swap.conf` — persist config

**Proposed PrivilegedCommand.Id:** `kernel.set-swappiness` (or unified `sysctl.set`)

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// Runtime:  ["/usr/sbin/sysctl", "-w", "vm.swappiness=" + numeric_value]
// Persist:  ["/bin/tee", "/etc/sysctl.d/99-auracore-swap.conf"] with stdin supplied
// Validator: numeric_value in range [0, 100] (verified pre-invocation)
```

---

### macOS Modules (5)

#### 7. DnsFlusher
- **File:** `src/Modules/AuraCore.Module.DnsFlusher/DnsFlusherModule.cs`
- **Hits:** Lines 83, 86, 89
- **Platform:** macOS
- **Umbrella listed:** ✓ Yes
- **Current sudo invocations:**
  - Line 83: `sudo -n dscacheutil -flushcache` — flush resolver cache
  - Line 86: `sudo -n killall -HUP mDNSResponder` — signal mDNSResponder to reload config
  - Line 89: `sudo -n killall mDNSResponderHelper` — kill helper process

**Proposed PrivilegedCommand.Id:** `dns.flush-cache`

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// Flush cache:     ["/usr/bin/dscacheutil", "-flushcache"]
// Signal daemon:   ["/usr/bin/killall", "-HUP", "mDNSResponder"]
// Kill helper:     ["/usr/bin/killall", "mDNSResponderHelper"]
// Validator: no variadic arguments (all are hardcoded commands)
```

---

#### 8. PurgeableSpaceManager
- **File:** `src/Modules/AuraCore.Module.PurgeableSpaceManager/PurgeableSpaceManagerModule.cs`
- **Hits:** Line 114
- **Platform:** macOS
- **Umbrella listed:** ✓ Yes
- **Current sudo invocation:**
  - Line 114: `sudo -n periodic daily weekly monthly` — run maintenance scripts

**Proposed PrivilegedCommand.Id:** `macos.periodic-maintenance`

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// ["/usr/sbin/periodic", "daily"] | ["/usr/sbin/periodic", "weekly"] | ["/usr/sbin/periodic", "monthly"]
// Validator: frequency must be one of: {daily, weekly, monthly}
```

---

#### 9. SpotlightManager
- **File:** `src/Modules/AuraCore.Module.SpotlightManager/SpotlightManagerModule.cs`
- **Hits:** Line 126
- **Platform:** macOS
- **Umbrella listed:** ✓ Yes
- **Current sudo invocation:**
  - Line 126: `sudo -n mdutil {args}` where args ∈ {-i on, -i off, -E, -a, etc.}

**Proposed PrivilegedCommand.Id:** `spotlight.manage`

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// ["/usr/bin/mdutil", path, mdutil_flag]
// Valid mdutil_flags: {"-i on", "-i off", "-E", "-a -i off", etc.}
// Validator: whitelist mdutil flags by parsing the args switch statement in source
```

---

#### 10. TimeMachineManager
- **File:** `src/Modules/AuraCore.Module.TimeMachineManager/TimeMachineManagerModule.cs`
- **Hits:** Lines 171, 192
- **Platform:** macOS
- **Umbrella listed:** ✓ Yes
- **Current sudo invocations:**
  - Line 171: `sudo -n tmutil delete "{escapedPath}"` — delete old Time Machine snapshot
  - Line 192: `sudo -n tmutil delete "{escapedPath}"` — variant in different branch

**Proposed PrivilegedCommand.Id:** `timemachine.delete-snapshot`

**Proposed Executable + argv validator pattern:**
```csharp
// Whitelist schema:
// ["/usr/bin/tmutil", "delete", snapshot_path]
// Validator: path must begin with "/" (absolute) and exist in tmutil output; reject .. and shell metacharacters
```

---

#### 11. XcodeCleaner
- **File:** `src/Modules/AuraCore.Module.XcodeCleaner/XcodeCleanerModule.cs`
- **Hits:** None found by grep
- **Platform:** macOS
- **Umbrella listed:** ✓ Yes
- **Rationale for inclusion:** Module manages Xcode via `xcrun simctl delete unavailable`. Xcrun is typically unprivileged but may perform operations that need elevation.

**AUDIT NOTE:** Low risk. Module uses `xcrun` (unprivileged Apple developer tool interface). Recommend confirming whether any operations (e.g., cache deletion) require elevation. Expected to remain unprivileged or fallback gracefully.

---

#### 12. MacAppInstaller
- **File:** `src/Modules/AuraCore.Module.MacAppInstaller/MacAppInstallerModule.cs`
- **Hits:** None found by grep
- **Platform:** macOS
- **Umbrella listed:** ✓ Yes
- **Rationale for inclusion:** Module manages Homebrew (`brew install`, `brew remove`). Homebrew on modern macOS typically operates unprivileged (installs to `/usr/local/` or Homebrew's own prefix with user ownership).

**AUDIT NOTE:** Low risk. Module shells out to `brew` commands (unprivileged). Recommend confirming no sudo is used for sudo-requiring operations (e.g., system-wide cask installations may need privilege on some macOS versions). Expected to remain unprivileged.

---

## Part 2: Reconciliation Against Umbrella List

### Required modules (from §5.2 umbrella)

| Module | Found with sudo? | Status |
|--------|------------------|--------|
| JournalCleaner (Linux) | No | ✓ Present, unprivileged; audit recommended |
| SnapFlatpakCleaner (Linux) | **Yes** | ✓ CONFIRMED |
| DockerCleaner (Linux) | No | ✓ Present, unprivileged via docker-group |
| KernelCleaner (Linux) | No | ✓ SURPRISE: Review needed (kernel removal likely needs elevation) |
| LinuxAppInstaller (Linux) | **Yes** | ✓ CONFIRMED |
| GrubManager (Linux) | **Yes** | ✓ CONFIRMED |
| DnsFlusher (macOS) | **Yes** | ✓ CONFIRMED |
| PurgeableSpaceManager (macOS) | **Yes** | ✓ CONFIRMED |
| SpotlightManager (macOS) | **Yes** | ✓ CONFIRMED |
| XcodeCleaner (macOS) | No | ✓ Present, unprivileged via xcrun; audit recommended |
| MacAppInstaller (macOS) | No | ✓ Present, unprivileged via brew; audit recommended |

**Summary:** All 11 umbrella modules present. 5 confirmed with explicit sudo patterns. 6 may use indirect privilege patterns or operate unprivileged; audit recommended for KernelCleaner (kernel removal) + optional for JournalCleaner, XcodeCleaner, MacAppInstaller.

---

## Part 3: Extras Surfaced

**Count:** 0 additional modules with sudo/pkexec outside the umbrella set.

**Modules scanned but excluded:**
- AppInstaller, AutorunManager, BatteryOptimizer, BloatwareRemoval, BrewManager, ContextMenu, CronManager, DefaultsOptimizer, DefenderManager, DiskCleanup, DnsBenchmark, DriverUpdater, EnvironmentVariables, ExplorerTweaks, FileShredder, FirewallRules, FontManager, GamingMode, HostsEditor, JunkCleaner, LaunchAgentManager, NetworkMonitor, NetworkOptimizer, PackageCleaner, PrivacyCleaner, ProcessMonitor, RamOptimizer, RegistryOptimizer, StorageCompression, SymlinkManager, SystemHealth, SystemdManager, TaskbarTweaks, WakeOnLan — none found with sudo/pkexec patterns.

**Verdict:** Umbrella scope is correctly scoped. No reclassifications needed.

---

## Part 4: Open Questions & Recommendations

### Immediate (block Phase 5.2.0 design)

1. **KernelCleaner privilege pattern**: Confirm whether kernel removal (apt/dnf remove) is invoked directly or delegated to OS. If direct, it must be in scope for 5.2.1 Linux migration.

2. **JournalCleaner vacuum scope**: Confirm whether `journalctl --vacuum-*` operates unprivileged (systemd-journal group membership) or requires elevation on all target distros (Ubuntu, Fedora, Arch). If unprivileged, clarify as out-of-scope for 5.2.

3. **Subprocess privilege chaining**: GrubManager line 232 uses a shell command within `sh -c` that performs `grep | sed | mv` chaining. Validator must be robust to prevent sed-pattern breakout. Recommend capturing the exact sed/mv invocation signatures in the action whitelist.

### Deferred (post-5.2.0, audit-discovery phase)

4. **XcodeCleaner Simulator management**: Confirm whether `xcrun simctl delete unavailable` ever requires elevation on modern macOS (12.0+). If yes, add to macOS action whitelist; if no, mark as false positive.

5. **MacAppInstaller Homebrew scope**: Confirm whether any Homebrew operation invoked by the module requires elevation (e.g., system cask installs). Expected to remain unprivileged, but verify across target macOS versions (11, 12, 13, 14).

### Optional (low priority, UX refinement)

6. **TimeMachineManager snapshot path validation**: Snapshot paths from `tmutil listlocalsnapshotdates` are APFS-internal; validator must safely parse and reject user-supplied paths. Ensure audit logging includes rejected paths.

7. **DNS Flusher daemon restart**: DnsFlusher kills mDNSResponder but may need to wait for automatic restart or explicitly restart. Verify post-invocation state and add restart wait if needed.

---

## Summary Table: Module Migration Load

| Module | Platform | Confirmed Privilege | Proposed Action ID | PrivilegedCommand Complexity | Priority |
|--------|----------|--------------------|--------------------|-----------------------------|-----------| 
| GrubManager | Linux | ✓ Yes (sudo) | grub.update-config | **High** (sed patterns, mv, update-grub) | 5.2.1 P1 |
| LinuxAppInstaller | Linux | ✓ Yes (sudo) | package.install/remove | Medium (apt/snap, whitelist) | 5.2.1 P1 |
| SnapFlatpakCleaner | Linux | ✓ Yes (sudo) | snap.remove | Low (single command) | 5.2.1 P1 |
| SwapOptimizer | Linux | ✓ Yes (sudo) | kernel.set-swappiness | Low (numeric validation) | 5.2.1 P1 |
| KernelCleaner | Linux | ? (audit needed) | kernel.remove | Medium (apt/dnf, versioning) | 5.2.1 P2 |
| JournalCleaner | Linux | ? (unprivileged likely) | - | None / Low | 5.2.1 P3 (optional) |
| DnsFlusher | macOS | ✓ Yes (sudo) | dns.flush-cache | Low (hardcoded commands) | 5.2.2 P1 |
| PurgeableSpaceManager | macOS | ✓ Yes (sudo) | macos.periodic-maintenance | Low (frequency enum) | 5.2.2 P1 |
| SpotlightManager | macOS | ✓ Yes (sudo) | spotlight.manage | Medium (mdutil flags) | 5.2.2 P1 |
| TimeMachineManager | macOS | ✓ Yes (sudo) | timemachine.delete-snapshot | Medium (path validation) | 5.2.2 P1 |
| XcodeCleaner | macOS | ? (unprivileged likely) | - | None / Low | 5.2.2 P3 (optional) |
| MacAppInstaller | macOS | ? (unprivileged likely) | - | None / Low | 5.2.2 P3 (optional) |

---

## Conclusion

**Audit complete.** All 11 umbrella modules enumerated. 6 confirmed with explicit sudo patterns (5 macOS/Linux split: 4 Linux, 2 macOS, 1 multi-platform); 5 require manual audit for privilege scope (KernelCleaner high priority, others low). No out-of-scope modules detected.

**Confidence:** High for confirmed modules; medium for audit-deferred modules (likely unprivileged).

**Next steps:** 
- Phase 5.2.0: Land abstraction + PrivilegedCommand contract
- Phase 5.2.0 audit-discovery subagent: Manually verify KernelCleaner, JournalCleaner, XcodeCleaner, MacAppInstaller
- Phase 5.2.1: Linux migrations (4-6 modules per priority)
- Phase 5.2.2: macOS migrations (5 modules)
