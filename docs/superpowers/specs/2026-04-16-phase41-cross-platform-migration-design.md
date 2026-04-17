# Phase 4.1 — Cross-Platform Module Migration

**Date:** 2026-04-16
**Branch:** `phase-4-module-pattern` (continues from Phase 4.0 milestone `e8e5a30`)
**Status:** Design approved, ready for implementation

---

## 1. Scope

Migrate 9 existing Linux/macOS/shared modules to the Phase 4.0 shell pattern (ModuleHeader + StatCard/StatRow + GlassCard). Identical methodology to Phase 4.0 spec §7. No new primitives needed.

### Modules (9)

| # | Module | Platform | AXAML lines | Archetype |
|---|--------|----------|-------------|-----------|
| 1 | DefaultsOptimizerView | macOS | 51 | List+Toggle |
| 2 | LaunchAgentManagerView | macOS | 76 | List+Toggle |
| 3 | BrewManagerView | macOS | 78 | List+Action |
| 4 | PackageCleanerView | Linux | 81 | Action+Result |
| 5 | SystemdManagerView | Linux | 83 | List+Toggle |
| 6 | TimeMachineManagerView | macOS | 84 | Action+Status |
| 7 | SymlinkManagerView | Win+Linux | 86 | Scan+Report |
| 8 | CronManagerView | Linux | 95 | List+Editor |
| 9 | SwapOptimizerView | Linux | 96 | Action+Status |

Ordered easy → hard by AXAML line count.

### In scope
- 9 module XAML rewrites using Phase 4.0 primitives
- ~32 smoke tests (3-4 per module)
- V1→V2 token migration in each module
- Code-behind preservation (zero diff)

### Out of scope
- New primitives (Phase 4.0's 3 suffice)
- Visual verification on Linux/macOS VMs (deferred to Phase 4.3+ feature dev)
- 13 new modules promised on landing page (Phase 4.3-4.4)
- V1 theme bridge removal (Phase 4.5)

## 2. Cross-platform verification approach

These modules are platform-guarded (only appear in sidebar on their OS). On Windows:
- **XAML compile:** works (Avalonia cross-platform) ✅
- **Headless smoke tests:** works (Avalonia.Headless is platform-agnostic) ✅
- **Visual verification:** impossible (modules not navigable in sidebar on Windows) ❌

Compile + headless tests are sufficient. Visual verify happens when Phase 4.3+ builds real features for these platforms.

## 3. Methodology

Identical to Phase 4.0 spec §7 (7-step per-module checklist). Reference that spec — no duplication.

## 4. Success criteria

- 9 modules migrated, each using ModuleHeader + GlassCard
- ~32 new smoke tests green
- Full suite zero regression (523 → ~555)
- Code-behind zero diff for all 9 modules
- No V1 bridge keys in new XAML
- Size reduction NOT a criterion (Phase 4.0 lesson learned)

## 5. Deliverables

~10 atomic commits on `phase-4-module-pattern` branch. Milestone commit at end.
