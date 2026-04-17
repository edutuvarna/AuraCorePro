# Phase 3 Core Complete — Session Handoff (2026-04-15)

**Branch:** `phase-3-ai-features` · **HEAD:** `d6f5d63` · **Tests:** 456/456 green (full solution)

> Full details: `~/.claude/projects/C--/memory/project_ui_rebuild_phase_3_core_complete.md`
> Plan: `docs/superpowers/plans/2026-04-15-phase3-ai-features-consolidation.md`
> Spec: `docs/superpowers/specs/2026-04-15-phase3-ai-features-consolidation-design.md`

---

## Completed in this session (33 tasks + Phase 2.5 hotfixes)

1. **Phase 2.5 hotfixes** (on `phase-2-sidebar-dashboard`)
   - `bdcb0fc` — QuickActionTile Button `HorizontalAlignment="Stretch"` (4 tiles now equal width)
   - `a6eded8` — Dashboard GPU polling moved to background thread (UI freeze fix) + CPU metric replaced with PerformanceCounter (Windows) / `/proc/stat` delta (Linux)

2. **Phase 3 Core** (on `phase-3-ai-features`, 33 commits)
   - Services/AI layer: ModelDescriptor, IModelCatalog (8 models), IInstalledModelStore, IModelDownloadService (R2 + size verify + UA bypass), ICortexAmbientService, ITierService (historical Pro-tier mapping recovered)
   - AppSettings with crash-safe JSON persistence + 7 Phase 3 properties
   - AIFeatureCardVM, AIFeaturesViewModel (Overview/Detail modes)
   - AIFeatureCard UserControl + AIFeaturesView.axaml shell
   - SidebarNavItem IsLocked DP + `:locked` pseudoclass + PART_LockIcon
   - SidebarViewModel: single "✦ AI Features [CORTEX]" link, tier lock wire-up, 10 missing modules added (system-health, admin-panel, 4 Linux, 4 macOS)
   - 4 sections moved to Views/Pages/AI/ (Schedule, Insights, Recommendations, Chat with warning banner + model chip placeholder)
   - SmartOptimizePlaceholderDialog deleted (CTA routes to AIFeaturesView)
   - ChatOptInDialog (2-step: acknowledge → model select) + ModelManagerDialog (OptIn/Manage modes, RAM-aware)
   - Chat toggle wired → opens ChatOptInDialog on first enable
   - TierUpgradePlaceholderDialog + MainWindow locked-item click handler
   - Localization EN + TR: 83 keys × 2 locales

3. **Infrastructure** (external, set up during brainstorming)
   - R2 bucket `auracore-models` (ENAM) with 8 GGUF models
   - Custom domain `models.auracore.pro` + SSL + Cache Rule 1y + HSTS + TLS 1.2 + Bot Fight Mode

---

## Deferred to next session (4 tasks + visual QA)

### Task 31 — Chat header model chip dropdown (polish)
Replace placeholder Button in ChatSection with SplitButton + MenuFlyout showing installed models + "Download more..." entry. Plan §Task 31 has full template.

### Task 32 — DashboardViewModel ripple (user-visible)
Subscribe to ICortexAmbientService. Wire `ShowCortexInsightsCard`, `CortexChipState`, `SmartOptimizeEnabled`, `ShowCortexSubtitle` — toggling Insights/Recommendations OFF changes Dashboard visibly. Plan §Task 32.

### Task 33 — StatusBar ripple
Bind status bar CORTEX message to `ICortexAmbientService.AggregatedStatusText`. Simple once Task 32 patterns established. Plan §Task 33.

### Task 38 — Manual visual QA sweep
15-step checklist from spec §11.5. Launch app, walk through each step. Expected to catch any visual regressions. **RECOMMEND doing this first** in next session — cheaper than debugging after polish.

---

## Remaining fixes (non-blocking debt)

| # | Item | Fix in |
|-|-|-|
| 1 | `TextTransform="Uppercase"` converter missing — AIFeatureCard kicker shows mixed case | Phase 5 polish |
| 2 | CortexAmbientService.ComputeStatusText() returns hardcoded EN strings | Phase 5 (wire to LocalizationService using already-added keys) |
| 3 | SidebarCategoryVM.Badge data added but XAML chip still driven by IsAccent flag | Phase 4/5 |
| 4 | Smart Optimize CTA lands on AIFeaturesView overview, not Recommendations section | Task 32 (pending-section hint through NavigateToModule) |
| 5 | AppSettings not thread-safe, no INotifyPropertyChanged | Phase 5 (add lock/SemaphoreSlim + NPC; was flagged in Task 3 review) |
| 6 | UserSession → SidebarViewModel tier hardcoded `UserTier.Free` in DI | Phase 5 (Settings/Onboarding cohesion) |
| 7 | HSTS `preload` directive on origin Nginx — safe today but risky if ever submitted to hstspreload.org | Phase 5 or ops-time cleanup |

Pre-existing (NOT Phase 3 scope):
- `AuraCore.Desktop.csproj` build error (Windows App SDK packaging DLL — environment issue)
- 138 pre-existing CA1416/CS0067 warnings in Phase 1/2 code

---

## Remaining phases (per Vision Doc §10)

### Phase 4 — Module Pages Refactor (~3-4 weeks)

- 26 module pages migrate to card-based layout with Phase 1 primitives
- Remove V1 theme bridge (`AuraCoreTheme.axaml`)
- Batched: 5-7 modules per sub-phase
- Candidates for Phase 4 OR dedicated mini-phase between 3-4:
  - Settings > Models page (Phase 3 deferred advanced features: delete, disk usage, auto-update check, checksum validation, pause/resume download, bandwidth throttling)

### Phase 5 — Advanced / Polish (~1 week)

- Settings/Onboarding/Login cohesion
- Animations (scan/optimize completion bloom, accordion ease, hover micro-interactions, neon glow transitions)
- Platform-specific refinements (Windows Mica, macOS vibrancy, Linux gradient fallback)
- All Phase 3 deferred polish items naturally land here (see "Remaining fixes" table above)

### Out of scope (deferred indefinitely per Vision Doc §12)

- Light theme
- Localization beyond EN/TR
- Mobile/tablet layouts
- Deep-link URL routing (`auracore://...`)
- Visual regression tests (screenshot diff)

---

## Business-model note: tier locking restoration

**IMPORTANT:** Phase 3 restored the historical Pro-tier mapping that was accidentally dropped in Phase 2. When next session runs the app as Free tier user, they'll see NEW lock icons on these 8 modules:

- `storage-compression`, `registry-optimizer`, `bloatware-removal`, `context-menu`, `disk-cleanup`, `privacy-cleaner`, `iso-builder`, `driver-updater`

This is **intentional** — it's the restored business model from pre-Phase-2 code (commit `7566d04` in git history). Not a regression.

Admin tier users (`admin.auracore.pro` credentials) bypass all locks.

Click a locked module → `TierUpgradePlaceholderDialog` shows "This feature requires Pro tier. Contact admin to upgrade." (placeholder — full upgrade UX is Phase 5).

---

## Bootstrap next session

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git checkout phase-3-ai-features  # should be at d6f5d63
git log --oneline -3

# Baseline: 456/456 green
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!"

# Kill any running app (file locks prevent rebuild)
taskkill /F /IM AuraCore.Pro.exe 2>nul || echo "not running"

# Visual verify BEFORE starting Task 31:
dotnet run --project src/UI/AuraCore.UI.Avalonia/AuraCore.UI.Avalonia.csproj --configuration Debug
```

Walk through these 6 checks:
1. Dashboard renders, Quick Actions 4 tiles evenly stretched
2. Sidebar shows "✦ AI Features [CORTEX]" as single link
3. Click AI Features → hero + 2×2 grid
4. Click any card → detail mode (left sidebar + content pane)
5. Toggle Chat ON → ChatOptInDialog appears
6. 8 Pro-tier modules show lock icon, click → TierUpgradePlaceholderDialog

If all 6 pass → start Task 38 (full §11.5 QA) then Tasks 32, 33, 31 in that order.

If any fail → STOP, diagnose, fix before polish. Context in this session was tight so edge cases may exist.

---

## Plan-doc section references (for next session Claude)

Open `docs/superpowers/plans/2026-04-15-phase3-ai-features-consolidation.md` and search for:

- **"Task 31"** — Chat header model chip dropdown implementation
- **"Task 32"** — DashboardViewModel ripple implementation
- **"Task 33"** — StatusBar ripple implementation
- **"Task 38"** — Manual visual checks

All three polish tasks have complete code templates with tests. No new design decisions needed.
