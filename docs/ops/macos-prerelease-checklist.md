# macOS Pre-Release Checklist

**Purpose:** Run this checklist sequentially before any AuraCorePro macOS release. All items must be ticked before the operator uploads the macOS .dmg via the admin panel. The checklist is operator-driven — sign and date it on completion. Future Phase 6.18+ may automate parts (CI build hygiene, `Avalonia.Headless` sidebar tests).

**Created:** Phase 6.16 (post-Linux-smoke-test disaster). Ensures macOS does not ship with the same surprise — 7 hard crashes / 4 silent Dashboard-fallbacks / 3 broken renders / 11 Windows-centric strings — that the Linux smoke test surfaced for v1.8.0.

**Source spec:** `docs/superpowers/specs/2026-04-28-phase-6-16-linux-platform-awareness-design.md` § D6.

---

## Build hygiene

- [ ] `dotnet build AuraCorePro.sln -c Release` exits with **0 CA1416 warnings/errors**
- [ ] All Windows-only module classes have `[SupportedOSPlatform("windows")]` attribute (verify via `grep -rn 'class.*Module : IOptimizationModule' src/Modules/AuraCore.Module.* | xargs -I {} bash -c 'grep -B1 "{}" {}.cs | grep -q SupportedOSPlatform && echo OK || echo MISSING'`)
- [ ] All Linux-only module classes have `[SupportedOSPlatform("linux")]` attribute (NEW for 6.17+ — Phase 6.16 deferred; revisit before macOS release)
- [ ] All macOS-only module classes have `[SupportedOSPlatform("macos")]` attribute (NEW for macOS release)
- [ ] All `[DllImport]` declarations have `[SupportedOSPlatform]` matching the target

## Sidebar correctness

- [ ] Every module declared in `SidebarViewModel.cs` has correct `platform` value matching `IOptimizationModule.Platform`
- [ ] Run the SidebarDeclarationConsistencyTests on a macOS build — assert sidebar items count matches expected macOS module list
- [ ] No `Windows` / `Linux` / `Ubuntu` / `Win` / `apt-get` text leakage in macOS-visible labels (run `dotnet test --filter HardcodedStringScanner` — the Phase 6.16 platform-name leakage test must pass)
- [ ] On macOS, sidebar shows: ~9 macOS-specific modules (DefaultsOptimizer, LaunchAgentManager, BrewManager, TimeMachineManager, XcodeCleaner, DnsFlusher, PurgeableSpaceManager, SpotlightManager, MacAppInstaller) + ~12 cross-platform (Dashboard, Settings, AIFeatures, RamOptimizer, JunkCleaner, DiskCleanup, PrivacyCleaner, FileShredder, HostsEditor, SymlinkManager, ProcessMonitor, SystemHealth, SpaceAnalyzer, WakeOnLan, AdminPanel, EnvironmentVariables) + DockerCleaner (Linux+macOS shared)

## Runtime smoke (real Mac required)

- [ ] App launches without crash (cold start to MainWindow visible)
- [ ] Click each macOS-specific module — opens proper view, no crash
- [ ] Click each cross-platform module — opens proper view, no crash
- [ ] Each Linux- and Windows-only module is **hidden** from sidebar
- [ ] Each module returning `RuntimeUnavailable` shows `UnavailableModuleView` with actionable remediation (e.g. `brew install X`)
- [ ] `BackgroundScheduler` runs for ≥3 minutes without throwing (timer-tick trap — Phase 6.16 Wave B Task 10 fixed this for Linux; verify it holds on macOS)
- [ ] AI Features panel opens and chat renders (LLM model load on macOS verified)
- [ ] Settings page version label reads "1.x.0 (Avalonia Cross-Platform)" with no Windows-centric copy
- [ ] Dashboard QuickActions tiles: 2 visible on macOS (`quick-cleanup`, `optimize-ram`); `remove-bloat` is filtered out (Phase 6.16 Wave E Task 19)

## Per-module pass/fail matrix

Run a matrix similar to `docs/superpowers/phase-6-16-vm-verify-matrix.md` but tailored for macOS module list. For each: Sidebar visible (yes/no), Crashed (yes/no), Render (OK / Unavailable / Dashboard-fallback). All Crashed=no, no Dashboard-fallbacks. Windows+Linux only modules MUST NOT be visible.

## macOS-specific

- [ ] Apple Developer ID signature applied to .app bundle (`codesign -dv` shows valid)
- [ ] Notarization request submitted to Apple, approval received
- [ ] DMG created via `packaging/build-macos.sh`, smoke-tested on a separate clean macOS VM/machine
- [ ] Gatekeeper assessment passes (`spctl -a -v <path>.app` exits 0)
- [ ] First-run does not show "developer cannot be verified" warning
- [ ] App icon renders correctly (no missing icon placeholder)

## Localization

- [ ] All 11 platform-neutral keys (per Phase 6.16 Wave E Task 18) verified on macOS UI
- [ ] No `Windows` / `Linux` string leaks to a macOS-visible UI surface
- [ ] Onboarding flow tested end-to-end on macOS (login → tier selection → dashboard)
- [ ] TR (Turkish) localization tested too — switch language, verify same coverage

## CHANGELOG + version

- [ ] CHANGELOG entry added for macOS support
- [ ] Version bumped (likely v1.9.0 with macOS as headline feature)
- [ ] All version-bump locations updated (per Phase 6.15.7 lessons — 11+ locations across `csproj`, plist, packaging scripts)

## Distribution

- [ ] `cross-publish.ps1` and/or `build-macos.sh` produces signed .dmg artifact
- [ ] R2 upload via admin panel: macOS platform tile enabled (currently "Coming Soon" in admin UI — Phase 6.17 prep work)
- [ ] GitHub Releases mirror includes macOS artifact
- [ ] Landing page OS-detect serves macOS DMG to Mac visitors
- [ ] Update endpoint `/api/updates/check?platform=macos` returns the new release
- [ ] SHA256 of the .dmg recorded in admin panel

## Sign-off

- Operator: \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
- Date: \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
- Build SHA: \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
- Mac hardware used for smoke: \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_

---

## When this checklist runs

- **Triggers:** any commit on `main` that bumps the macOS version OR introduces a macOS-specific module/feature OR is the first build after `[SupportedOSPlatform("macos")]` annotations land.
- **Blocks:** v1.x release where x ≥ 9 if macOS is in scope.
- **Out of scope:** Linux + Windows continue to use their respective release flows (`docs/superpowers/phase-6-16-vm-verify-matrix.md` for Linux; standard Windows release for Windows). This document is macOS-only.

## Future automation (Phase 6.18+)

- CI gate: a macOS-runner GitHub Action that auto-runs the **Build hygiene** + **Sidebar correctness** sections (build + headless tests). Manual sections (Runtime smoke + Distribution) remain operator-driven until we have on-runner macOS click-through automation (likely Apple's `xcuitest` or similar).
