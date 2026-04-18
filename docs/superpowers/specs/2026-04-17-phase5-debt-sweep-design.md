# Phase 5 Debt Sweep Mini-Wave Design

**Date:** 2026-04-17
**Branch:** NEW — `phase-5-debt-sweep` from `main` HEAD `cf5330c` (Phase 5 Wave 3 merge)
**Status:** Scope approved; user-interactive brainstorm. Locked 8 items across 2 sub-waves.

---

## 1. Context

Phase 5 Wave 3 merged to `main` at `cf5330c`. Test baseline: 2141 passing + 6 skipped. Memory snapshot: [`project_ui_rebuild_phase_5_close.md`](../../../C:/Users/Admin/.claude/projects/C--/memory/project_ui_rebuild_phase_5_close.md).

The Phase 5 close memory lists ~20 carry-forward debt items. Not all are actionable in a short mini-wave — some need real macOS hardware (notarization, shim libs), some are new-feature-scope (Driver full install via WU Agent COM API, Linux/macOS QuickAction presets), some are too big (pixel-regression infrastructure). This mini-wave takes a high-impact subset per user's "Option B" scope selection (2-3 days, ~8-10 tasks).

---

## 2. Scope — 8 items, 2 sub-waves

### Sub-wave A — User-visible completions (3 items)

| # | Item | Size | Rationale |
|---|------|------|-----------|
| A1 | ChatSection.axaml.cs ReloadAsync UI wiring | S | VM hook lands Phase 5.4 (`bd47650`); UI still advises restart. Replace with actual `_llm.ReloadAsync(newConfig)` call + IsReloading-bound spinner. |
| A2 | Windows Named Pipe end-user install UX (first-run elevation prompt) | M | **Shipability gate**: end users cannot use Driver/Defender/Service write features without running `scripts/install-privileged-service.ps1` as admin. Need an in-app "Install privileged helper?" consent dialog that triggers UAC-elevated script execution. |
| A3 | WorstTemp live data in DiskHealthSummaryCard | S | Phase 5.5 shipped with "—" placeholder. Swap `DriveInfo`-only scan for WMI `MSStorageDriver_ATAPISmartData` query in `DiskHealthScanner`. |

### Sub-wave B — Test health + ops hygiene + Linux whitelist (5 items)

| # | Item | Size | Rationale |
|---|------|------|-----------|
| B1 | UI test harness flake root cause | M | Every `dotnet test` run has 0-4 random UI test fails on first pass; second pass clean. Silent reliability debt; will break CI the moment we add CI. Needs investigation — likely Avalonia headless timing / shared-static state. |
| B2 | 6 skipped pilot-view render tests (Phase 5.3.3) | S | `SystemHealth` / `BloatwareRemoval` / `RamOptimizer` views require `App.Services` DI root which isn't bootstrapped in headless test harness. Fix: minimal DI bootstrap in `AvaloniaTestApplication`. |
| B3 | Nginx `auracore-api.bak` retire (live SSH) | XS | Pre-existing stale file in `/etc/nginx/sites-enabled/` at origin `165.227.170.3`. Causes non-fatal "conflicting server name" warnings. Move to `sites-available/archive-20260417-api.bak`. |
| B4 | API conf duplicate `add_header Strict-Transport-Security` consolidation (live SSH) | XS | 3 locations in `auracore-api` conf → single directive. Pre-existing, not a regression. |
| B5 | GrubManager 3 deferred sudo hits (Linux) | S | Phase 5.2.1 carry-forward. Action whitelist additions: `grub-mkconfig`, `backup-etc-grub` in `AuraCore.PrivHelper.Linux/ActionWhitelist.cs` + `pro.auracore.privhelper.policy` mirror. Remove 3 `TODO(phase-5.2.1)` markers in `GrubManagerModule.cs`. |

---

## 3. Sub-wave sequencing

```
Sub-wave A (user-visible)  →  Sub-wave B (stability/hygiene)
```

**Rationale**: A items are user-facing; if we ship a point release tomorrow, we want them landed first. B items are maintenance and don't affect users directly. Sub-wave A milestone first locks user-visible scope; then B hardens the base.

No inter-dependencies between A and B that force strict order beyond preference.

**Inside sub-waves**, task order:
- A: A1 (smallest, warm-up) → A3 (UI polish) → A2 (biggest, requires new dialog + elevation plumbing)
- B: B5 (smallest, mechanical) → B3 + B4 (ops, can be single SSH session) → B2 (DI bootstrap, medium) → B1 (investigation, unknown depth — leave for last)

---

## 4. Locked decisions

### Q1 — Branch strategy
**Decision:** NEW branch `phase-5-debt-sweep` from `main` HEAD `cf5330c`. Don't reopen `phase-5-polish` (already deleted + merged).
**Why**: Clean slate; signals "Phase 5 Wave 3 is closed, this is follow-up".

### Q2 — macOS debt items?
**Decision:** **Excluded** from this mini-wave. TimeMachineManager deferred verbs, SwapOptimizer, daemon ObjC shim, SMAppService NSError shim, TEAM_ID substitution, notarization — all need real macOS hardware, not available.
**Why**: Cannot be meaningfully executed or tested without a Mac. Push to dedicated future macOS ship-prep wave.

### Q3 — B1 flake investigation approach
**Decision:** **Time-box**: 1 sub-task effort. If root cause found in that window, fix it. If not found or fix spills beyond plan granularity, document findings + mitigations (e.g., retry flag in test runner) and move on. Do NOT let B1 block the mini-wave milestone.
**Why**: Flake investigations can rabbit-hole. Better to land the other 7 items and escalate B1 to a dedicated follow-up if it's deep.

### Q4 — Test growth target
**Decision:** **~+20-35 tests** this wave:
- B2: +6 (reviving skipped pilot view tests) + maybe +2 for extended assertions
- A1: +3-5 (ReloadAsync UI-button click test — VM-level since full UI is headless flaky)
- A2: +5-8 (first-run dialog VM tests, install-state detection)
- A3: +3-5 (WMI mocked SMART data shape tests)
- B5: +3-5 (whitelist entries + polkit parity tests)
- B1/B3/B4: test neutral (flake fix may add 0-1 tests; ops are not code)

Target: 2141 → ~2170-2175 at close.

### Q5 — A2 (install UX) — UAC elevation mechanism?
**Decision:** **Shell-out to PowerShell with `-Verb RunAs`**. User clicks consent button → app invokes `powershell.exe -Verb RunAs -File scripts/install-privileged-service.ps1`. Windows shows standard UAC prompt; if user accepts, script runs elevated; app polls the pipe for availability post-install.

**Alternative considered:** Manifest-embedded `requestedExecutionLevel="requireAdministrator"` on a sibling elevator exe. More work (new csproj + manifest), same effective UX. Passed for now.

---

## 5. Architecture notes

### A1 wiring

`ChatSection.axaml.cs` currently has a comment: "IAuraCoreLLM has no Reload method today. Advise user to restart." That comment is now stale. Replace the restart-advice branch with:

```csharp
// Current (stale):
// "IAuraCoreLLM has no Reload method; advise restart"
// ShowToast("Model değişikliği restart sonrası etkili olur");

// Replace with:
try
{
    await _llm.ReloadAsync(newConfig, _reloadCts.Token);
    ShowToast("Model hot-swap başarılı");
}
catch (OperationCanceledException)
{
    ShowToast("Reload cancelled");
}
catch (InvalidOperationException)
{
    ShowToast("Another reload in progress");
}
```

Bind a spinner visibility to `IAuraCoreLLM.IsReloading` INPC.

### A2 install UX flow

```
App launch
   │
   ▼
First-run check: can connect to pipe "AuraCorePro"? (2s timeout)
   ├── YES → normal operation
   └── NO → show "Install privileged helper?" dialog on first privileged-action attempt (lazy trigger, NOT on app launch — avoids scaring users who never touch Driver/Defender/Service)
            │
            User accepts
            │
            ▼
            Spawn: powershell.exe -Verb RunAs -File "<app-dir>/scripts/install-privileged-service.ps1"
            │
            Post-script: poll pipe for 5s; if connects → "Installed" success toast; if not → "Install failed, see log" + link to log
```

Dialog surface: new `PrivilegedHelperInstallDialog.axaml` + VM. One-time consent; stores "declined"/"installed"/"not-asked" state in `AppSettings`.

### A3 WMI SMART data

Augment `DiskHealthScanner.ScanCore()` with a new branch on Windows:

```csharp
// Current:
// returns (Status, SmartBadge="OK"/"Warning"/"Fail", WorstTemp="—") using DriveInfo only

// Added:
// On Windows, attempt WMI query `SELECT * FROM MSStorageDriver_ATAPISmartData` in ROOT\WMI namespace.
// Parse VendorSpecific byte array; find temperature attribute (ID 0xC2 or 0xBE depending on drive).
// Convert to °C; surface worst drive's value as WorstTemp.
// On WMI-not-available (access denied, no SMART support), keep "—".
```

Graceful fallback is critical — WMI SMART query requires admin on some systems, so non-admin users see the placeholder.

### B1 flake investigation starting points

1. Grep test runs for which specific tests flake — collect 3-5 runs, match test names. Is it the same ones, or random?
2. Check for shared-static state in Avalonia test base classes (LocalizationService language, theme variant, DI container).
3. Check for Dispatcher.UIThread.Post() usages in test setup that might not have drained before assertions.
4. Consider if `[Collection]` attribute grouping is needed to serialize Avalonia-bound tests.

### B2 DI bootstrap

`AvaloniaTestApplication` currently omits `App.Services` bootstrap. Adding a minimal in-test `ServiceCollection` that registers just enough for the 3 pilot views (mock `INavigationService`, mock `IShellCommandService`, minimal `ILogger`-family) should unblock the 6 skipped tests. Keep it test-only; no production DI changes.

### B5 Linux whitelist additions

Two new action IDs in `ActionWhitelist.cs`:

```csharp
"grub-mkconfig" => new WhitelistedAction(
    Executable: "/usr/sbin/grub-mkconfig",
    AllowedArgumentPatterns: new[] { @"^-o$", @"^/boot/grub/grub\.cfg$" }),

"backup-etc-grub" => new WhitelistedAction(
    Executable: "/bin/cp",
    AllowedArgumentPatterns: new[] { @"^-a$", @"^/etc/default/grub$", @"^/etc/default/grub\.bak\.[0-9]{8}-[0-9]{6}$" }),
```

Mirror in `pro.auracore.privhelper.policy` with `auth_admin_keep`. Then in `GrubManagerModule.cs` lines 199/320/373, replace `sudo` calls with `_shell.RunPrivilegedAsync(new PrivilegedCommand("grub-mkconfig", ...))`.

---

## 6. Success criteria

- [ ] All 8 items addressed (or B1 explicitly documented as carry-over if time-boxed investigation fails to find root cause).
- [ ] 2 sub-wave milestone commits on `phase-5-debt-sweep`.
- [ ] Full solution tests green after each milestone (2141 → ~2170+ at close).
- [ ] No new NuGet packages.
- [ ] Memory file written: `project_ui_rebuild_phase_5_debt_sweep_complete.md`.
- [ ] MEMORY.md index updated: demote Phase 5 close to superseded; add debt sweep close.
- [ ] Branch mergeable to `main` when user ready (same `--no-ff` pattern as Wave 3).

---

## 7. Out of scope

- macOS-specific debt (notarization, deferred verbs, daemon shims, TEAM_ID) — future Mac ship-prep wave.
- Driver full install execution (WU Agent COM API) — new feature, Phase 6+ material.
- Linux / macOS QuickAction preset catalogs — new feature.
- AIFeaturesView 80 DIP icon-only compression — cosmetic; not user-flagged issue.
- Pixel-regression / visual snapshot infrastructure — too big; separate dedicated effort.
- AuraCore.Desktop WinAppSDK build error — pre-existing since Phase 5.1; low priority, non-blocking.
- Real VM validation — manual QA scope.

---

## 8. Rollback strategy

Two sub-wave milestone commits enable per-sub-wave rollback. Full mini-wave rollback: `git reset --hard cf5330c` returns to `main` merge state.

Live-ops rollback (B3/B4): Nginx `.bak` copies stashed under `sites-available/` before modifications.

---

## 9. Sign-off

Design approved 2026-04-17 by user (interactive). 8-item scope locked under "Option B" (high-impact subset). Skip spec review gate per `feedback_skip_spec_review.md`. Proceed directly to writing-plans.
