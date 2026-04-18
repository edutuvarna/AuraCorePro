# Handoff — Phase 6 Item 4 TR Completion Sweep (2026-04-18, post-AFK session)

**For next session — start here.** Phase 6 Items 2 + 3 shipped autonomously under AFK mode; Item 4 is next but warrants a fresh context budget. Everything below is what you need to pick it up cold.

## Current state

- **Branch:** `main` at `883d249` (local-only; origin still at `64c56ef`)
- **Test baseline:** **2261/2261 passing** + 0 skipped + 0 failed across 8 assemblies
- **Recent merges (this session):**
  - `e087700` — Phase 6.2 Pixel-Regression Testing infrastructure
  - `f931020` — Semantic tint hex AARRGGBB fix (first bug caught by 6.2)
  - `883d249` — Phase 6.3 Light Theme
- **Pending:** All local; `git push origin main` not run (user preference — previous items also landed locally first).

## Phase 6 Item 4 scope (per roadmap, user-locked)

> **TR completion sweep** — ensure every in-app string is Turkish when TR is selected (not just labels and sidebar). Status texts, dialog bodies, tooltips, error messages, deeper UI surfaces are currently EN-only even with TR selected. **This is a QA/completion sweep, not language-set expansion.** Only after full TR coverage is achieved should a 3rd language be considered.

**Deliverables:**
1. Audit the codebase for EN-only strings in UI surfaces (XAML `Text=`/`Content=`/`ToolTip=` + code-behind `string` literals that reach the UI).
2. Fix all offenders — add TR translations to `LocalizationService` and swap hardcoded literals for `LocalizationService.Get(...)` / XAML `{Binding S.Instance[key]}` bindings.
3. **Regression test harness** — scanner tests that assert no hardcoded user-facing string literals survive in UI XAML / code-behind. Fail if any module introduces a new EN-only string.

## Recommended execution approach

**Detection-first.** Build the regression test scanner FIRST, before fixing anything. Two reasons: (a) the scanner defines "what counts as a hardcoded string" — scope-locks the audit; (b) scanner output becomes the fix punch list.

Suggested sub-wave structure:

### 6.4.A — Build the XAML string-literal scanner
- xUnit test in `tests/AuraCore.Tests.UI.Avalonia/Localization/` that loads every `.axaml` file in `src/UI/AuraCore.UI.Avalonia/Views/**/*.axaml`, parses it, and asserts no `Text=`, `Content=`, `ToolTip=`, `Header=`, `Title=`, `Watermark=` attribute contains a non-binding, non-whitespace, non-numeric string value.
- Whitelist pattern for binding syntax: starts with `{`, `{Binding ...}`, `{DynamicResource ...}`, `{StaticResource ...}`, `{x:Static ...}`.
- Whitelist pure punctuation / symbols: `—`, `→`, `•`, `·`, etc. (add a small allowlist for design-system separators).
- Expected first run: **many failures** — this IS the audit.

### 6.4.B — Build the C# string-literal scanner (narrower)
- Scan `.cs` files under `src/UI/AuraCore.UI.Avalonia/Views/**` + `src/UI/AuraCore.UI.Avalonia/ViewModels/**` for string literals passed to `TextBlock.Text = "..."`, `Button.Content = "..."`, `MessageBox.Show("...")`, dialog Title = `"..."`, etc.
- More complex; may need Roslyn analyzer or careful regex. Narrower initial scope: only flag direct property assignments ending in `.Text` / `.Content` / `.Header` / `.Title` / `.Watermark`.
- Some false positives are OK — prioritize catching real offenders over zero-FP.

### 6.4.C — Fix module by module (sequential)
- Start with the highest-value surfaces: Dashboard, AIFeatures, Sidebar, Settings (most user-facing).
- Then modules in order of user-facing frequency: JunkCleaner, RAM, Bloatware, Defender, Service, Driver, DiskHealth, SpaceAnalyzer, etc.
- For each module:
  1. Run scanner → list of offenders.
  2. Add TR + EN strings to `LocalizationService.cs`.
  3. Replace literals with localization calls.
  4. Run app in both EN + TR, verify labels update.
  5. Run scanner → module passes.
  6. Commit.

### 6.4.D — Tooltip + dialog + error-message audit
- Sometimes missed in (A)/(B): `DispatchError("...")`, `CrashReportService.Log("...")`, notification toast strings.
- Add scanner rules for these patterns.
- Fix any found.

### 6.4.E — Ceremonial close
- Memory + MEMORY.md + milestone commit + merge.

## Bootstrap commands

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git checkout main                            # already at 883d249
git log --oneline -5                         # confirm recent Phase 6.3 merges
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!"
# Expected: 8 Passed! lines, 2261 total
git checkout -b phase-6-tr-completion        # new branch
```

## Read before starting

1. `C:/Users/Admin/.claude/projects/C--/memory/project_phase_6_roadmap.md` — strict scope lock on Item 4 (no 3rd language, TR coverage only).
2. `C:/Users/Admin/.claude/projects/C--/memory/project_phase_6_item_3_light_theme_complete.md` — immediate predecessor, shows ceremonial close pattern.
3. `src/UI/AuraCore.UI.Avalonia/LocalizationService.cs` — existing EN+TR key dictionary. ~950 keys today. Grep for `["..."]` to see the structure.

## Known concerns

1. **`LocalizationService` is a Dictionary<string, string>, not per-locale.** It's actually 2 dictionaries (EN map + TR map) keyed by string, with a static `_current` pointer. Adding new keys means adding to BOTH maps. Easy to forget one.
2. **Dynamic text updates.** Some views refresh their labels on `LocalizationService.LanguageChanged` event; others don't. Fixing a module may surface latent "doesn't update on language switch" bugs. Expect to wire `LanguageChanged` into a few views.
3. **Test harness false positives.** Design-system separators like `—` and `·` in `DesignSystemGallery` XAML will trigger the scanner — whitelist them or move them to string resources too.
4. **ThemeService Dark fallback + `AvaloniaTestApplication` Dark default.** The scanner tests don't need theme, but be aware that the test harness is locked to Dark; don't try to switch themes in scanner tests.

## AFK-mode continuation

User is in AFK mode as of session close (2026-04-18). `feedback_afk_default_recommended.md` applies:
- Skip clarifying questions, pick recommended defaults.
- Skip visual companion.
- Run brainstorm → plan → subagent-driven pipeline autonomously.
- Flag any GPU-intensive work and ASK before triggering (Item 4 doesn't need GPU).

## Estimated scope

- ~950 existing localization keys.
- Rough guess: **200-500 new TR strings** across 40-50 module XAMLs + code-behind.
- Subagent-friendly: each module is ~20-50 literals, fits a single implementer prompt cleanly.
- **Estimated effort:** 2-3 days (3-5 substantial sub-waves).

## Session-close note (for humans)

This AFK session autonomously shipped:
1. **Phase 6 Item 2** (Pixel-Regression Testing Infrastructure) — Verify.Xunit + Skia headless + `DesignSystemGallery` + 2 goldens. Scope pivoted from 15-view live-page coverage after discovering Dashboard's embedded OS-live-data. +2 tests.
2. **Semantic tint hex fix** — surfaced immediately by the new pixel regression gate. `SuccessBg/WarningBg/ErrorBg/InfoBg/AccentBg` tokens had trailing-alpha hex (`#RRGGBBAA` interpreted as ARGB olive/brown) instead of Avalonia's `#AARRGGBB`. Fixed all 10 tokens, re-accepted goldens.
3. **Phase 6 Item 3** (Light Theme) — Dark/Light/System switcher. ThemeService refactored from bool to 3-state AppTheme enum with OS-resolve. 27-key Light token dictionary sibling of Dark. Settings radio group. Phase 6.2 gallery extended with Light variant. +9 tests.

Net: **2250 → 2261 passing** (+11 across the session). 3 major merges to main.
