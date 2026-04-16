# Phase 4.1 — Cross-Platform Module Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 9 existing Linux/macOS/shared modules to Phase 4.0 shell pattern. Same methodology, same primitives, same rules.

**Architecture:** Identical to Phase 4.0. Each module gets: `controls:ModuleHeader` (title + subtitle + actions) → optional `controls:StatRow` + `controls:StatCard` → `controls:GlassCard` content wrapper(s). Code-behind untouched.

**Tech Stack:** Same as Phase 4.0. No new packages.

---

## Context & References

- **Phase 4.0 spec (methodology §7):** `docs/superpowers/specs/2026-04-16-phase4-module-pattern-design.md`
- **Phase 4.0 plan (precedent):** `docs/superpowers/plans/2026-04-16-phase4-module-pattern.md`
- **Primitives:** `ModuleHeader`, `StatCard`, `StatRow`, `GlassCard` in `xmlns:controls="using:AuraCore.UI.Avalonia.Views.Controls"`
- **V2 tokens:** StatusSuccessBrush, StatusErrorBrush, StatusWarningBrush, BorderSubtleBrush, BgCardBrush, FontSizeBody/BodySmall/Label/Display/Heading, TextPrimaryBrush, TextMutedBrush, RadiusMd, RadiusLg

## Codebase quirks

Same as Phase 4.0 plan — inherited. Key reminders:
1. Use `global::Avalonia` (namespace shadow)
2. `Dispatcher.UIThread.RunJobs()` for Loaded events in headless tests
3. V1 bridge keys (SuccessBrush, ErrorBrush, BorderCardBrush) FORBIDDEN in new XAML
4. GlassCard handles card border/background — don't add extra Border
5. Hidden TextBlocks for code-behind-driven stats (bind StatCard.Value via `{Binding #HiddenName.Text}`)

---

## Per-module task template

Every module follows the SAME steps. Subagent receives:
1. Read existing .axaml + .axaml.cs (extract x:Names + Click handlers)
2. Write smoke tests (Ctor, Render, ModuleHeader presence, GlassCard presence)
3. Rewrite .axaml (ModuleHeader + GlassCard + optional StatRow)
4. Build + test + full suite
5. Commit

---

## Task 1: Migrate DefaultsOptimizerView (macOS, 51 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/DefaultsOptimizerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/DefaultsOptimizerViewTests.cs`
- DO NOT TOUCH: `DefaultsOptimizerView.axaml.cs`

- [ ] **Step 1.1:** Read existing AXAML + code-behind, record x:Names and Click handlers
- [ ] **Step 1.2:** Write 4 smoke tests (Ctor, Render, ModuleHeader, GlassCard)
- [ ] **Step 1.3:** Rewrite .axaml with ModuleHeader + GlassCard shell
- [ ] **Step 1.4:** Build + run smoke tests + full suite
- [ ] **Step 1.5:** Commit: `refactor(ui): migrate DefaultsOptimizerView to Phase 4 shell`

---

## Task 2: Migrate LaunchAgentManagerView (macOS, 76 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/LaunchAgentManagerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/LaunchAgentManagerViewTests.cs`
- DO NOT TOUCH: `LaunchAgentManagerView.axaml.cs`

- [ ] **Step 2.1-2.5:** Same as Task 1 template. Commit: `refactor(ui): migrate LaunchAgentManagerView to Phase 4 shell`

---

## Task 3: Migrate BrewManagerView (macOS, 78 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/BrewManagerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/BrewManagerViewTests.cs`
- DO NOT TOUCH: `BrewManagerView.axaml.cs`

- [ ] **Step 3.1-3.5:** Same template. Commit: `refactor(ui): migrate BrewManagerView to Phase 4 shell`

---

## Task 4: Migrate PackageCleanerView (Linux, 81 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/PackageCleanerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/PackageCleanerViewTests.cs`
- DO NOT TOUCH: `PackageCleanerView.axaml.cs`

- [ ] **Step 4.1-4.5:** Same template. Commit: `refactor(ui): migrate PackageCleanerView to Phase 4 shell`

---

## Task 5: Migrate SystemdManagerView (Linux, 83 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SystemdManagerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/SystemdManagerViewTests.cs`
- DO NOT TOUCH: `SystemdManagerView.axaml.cs`

- [ ] **Step 5.1-5.5:** Same template. Commit: `refactor(ui): migrate SystemdManagerView to Phase 4 shell`

---

## Task 6: Migrate TimeMachineManagerView (macOS, 84 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/TimeMachineManagerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/TimeMachineManagerViewTests.cs`
- DO NOT TOUCH: `TimeMachineManagerView.axaml.cs`

- [ ] **Step 6.1-6.5:** Same template. Commit: `refactor(ui): migrate TimeMachineManagerView to Phase 4 shell`

---

## Task 7: Migrate SymlinkManagerView (Win+Linux, 86 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SymlinkManagerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/SymlinkManagerViewTests.cs`
- DO NOT TOUCH: `SymlinkManagerView.axaml.cs`

- [ ] **Step 7.1-7.5:** Same template. Commit: `refactor(ui): migrate SymlinkManagerView to Phase 4 shell`

NOTE: SymlinkManager is cross-platform (Win+Linux). It appears in sidebar on Windows too, so visual verification IS possible for this module.

---

## Task 8: Migrate CronManagerView (Linux, 95 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/CronManagerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/CronManagerViewTests.cs`
- DO NOT TOUCH: `CronManagerView.axaml.cs`

- [ ] **Step 8.1-8.5:** Same template. Commit: `refactor(ui): migrate CronManagerView to Phase 4 shell`

---

## Task 9: Migrate SwapOptimizerView (Linux, 96 lines)

**Files:**
- Modify: `src/UI/AuraCore.UI.Avalonia/Views/Pages/SwapOptimizerView.axaml`
- Create: `tests/AuraCore.Tests.UI.Avalonia/Views/SwapOptimizerViewTests.cs`
- DO NOT TOUCH: `SwapOptimizerView.axaml.cs`

- [ ] **Step 9.1-9.5:** Same template. Commit: `refactor(ui): migrate SwapOptimizerView to Phase 4 shell`

---

## Task 10: Validation + milestone commit

- [ ] **Step 10.1:** Full test suite: `dotnet test AuraCorePro.sln --verbosity minimal --nologo`
- [ ] **Step 10.2:** Code-behind preservation: `git diff e8e5a30 -- src/UI/AuraCore.UI.Avalonia/Views/Pages/{Defaults,LaunchAgent,Brew,Package,Systemd,TimeMachine,Symlink,Cron,Swap}*View.axaml.cs`
- [ ] **Step 10.3:** Milestone commit: `milestone: Phase 4.1 Cross-Platform Module Migration COMPLETE`

---

## Implementation Complete

After Task 10: Phase 4.1 shipped. Next: Phase 4.2 (remaining ~16 Windows modules, 2 batches of 7-8).
