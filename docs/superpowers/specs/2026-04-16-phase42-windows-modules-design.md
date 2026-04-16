# Phase 4.2 — Remaining Windows Module Migration

**Date:** 2026-04-16
**Branch:** `phase-4-module-pattern` (continues from Phase 4.1 milestone `0635743`)

## Scope

Migrate 24 remaining Windows modules to Phase 4.0 shell. Identical methodology. No new primitives.

### Modules (24, sorted by AXAML size)

| # | Module | Lines | Archetype |
|---|--------|-------|-----------|
| 1 | FontManagerView | 43 | List |
| 2 | DiskHealthView | 58 | Scan+Report |
| 3 | SpaceAnalyzerView | 58 | Scan+Report |
| 4 | RegistryOptimizerView | 65 | Action+Result |
| 5 | StartupOptimizerView | 65 | List+Toggle |
| 6 | ServiceManagerView | 67 | List+Toggle |
| 7 | WakeOnLanView | 71 | Action |
| 8 | NetworkMonitorView | 74 | Scan+Report |
| 9 | EnvironmentVariablesView | 75 | Editor |
| 10 | AdminPanelView | 76 | Dashboard-like |
| 11 | TweakListView | 87 | List+Toggle |
| 12 | BloatwareRemovalView | 91 | List+Action |
| 13 | FileShredderView | 103 | Action+Result |
| 14 | BatteryOptimizerView | 104 | Action+Status |
| 15 | DefenderManagerView | 116 | Multi-section |
| 16 | ScanOptimizeView | 120 | Scan+Action |
| 17 | CategoryCleanView | 130 | Multi-category |
| 18 | DriverUpdaterView | 146 | Scan+List |
| 19 | ProcessMonitorView | 156 | Live+Table |
| 20 | GamingModeView | 206 | Multi-toggle |
| 21 | AppInstallerView | 218 | Catalog+Install |
| 22 | NetworkOptimizerView | 302 | Multi-section |
| 23 | SystemHealthView | 311 | Multi-section |
| 24 | IsoBuilderView | 327 | Multi-step |

### Execution: 6 batches of 4

Same methodology as Phase 4.0 §7. 4 modules per subagent dispatch, ~24 smoke tests total.

### Success criteria: Same as Phase 4.1 (no size target).
