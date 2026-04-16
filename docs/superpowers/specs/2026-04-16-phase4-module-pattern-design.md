# Phase 4.0 — Module Page Pattern + Pilot Batch

**Date:** 2026-04-16
**Branch:** `phase-4-module-pattern` (from `phase-3-ai-features` HEAD `7045d5a`)
**Author:** Deniz Özgür + Claude Opus 4.6
**Status:** Design approved, ready for implementation plan

---

## 1. Context

Phase 3 shipped the AI Features consolidation and closed 2026-04-15/16. The codebase now has 481/481 tests green, 37 module views hand-coded with inconsistent patterns (some use V2 tokens, most mix hardcoded `#FFFFFF Opacity="X"` styling with `DynamicResource` brushes, none use the `GlassCard` primitive from Phase 1).

Vision Document §10 scopes Phase 4 as **Module Pages Refactor** — migrate ~26 module pages to a card-based layout using Phase 1 primitives, plus remove the V1 `AuraCoreTheme.axaml` bridge. Vision Doc recommends batching 5–7 modules per sub-phase.

This spec covers **Phase 4.0** — the foundational sub-phase that:
1. Establishes the "Module Page Shell" pattern shared by all subsequent batches.
2. Builds three new primitives (`ModuleHeader`, `StatCard`, `StatRow`).
3. Migrates a pilot batch of 5 Windows modules to validate the pattern.

Phase 4.1+ consumes the pattern to refactor remaining modules and (eventually) create 13 new Linux/macOS modules promised on the landing page.

---

## 2. Scope

### In scope

- 3 new primitives in `src/UI/AuraCore.UI.Avalonia/Views/Controls/`:
  - `ModuleHeader` — title + subtitle + right-aligned actions slot
  - `StatCard` — label + bindable value + accent brush
  - `StatRow` — horizontal equal-width container for StatCards
- ~18 unit tests for the three primitives (Phase 1 `AvaloniaFact` headless pattern)
- Migration of 5 pilot modules to the new shell:
  - `DnsBenchmarkView` (Action+Result archetype)
  - `RamOptimizerView` (Action+Result, stress-test with 3 header actions + custom progress bar)
  - `HostsEditorView` (Editor archetype, cross-platform-ready)
  - `FirewallRulesView` (List+Toggle with 3-stat row)
  - `AutorunManagerView` (List+Toggle)
- ~18 smoke tests for migrated modules (construction, no-crash, header title, optional stat count)
- Layout Variant A (Flat Header) reference implementation in all 5 pilots
- Per-module refactor checklist (methodology §7) usable as subagent prompt template by future batches

### Out of scope (forward-referenced)

- Layout Variant B (Hero Header) primitives — added in the first batch that needs them
- Layout Variant C (Toolbar + 2-col) primitives — added when a data-heavy module requires it
- V1 theme bridge (`AuraCoreTheme.axaml`) removal — dedicated batch after all modules migrated (Phase 4.5)
- Remaining 32 existing modules — Phase 4.1 (8 existing Linux/macOS) + Phase 4.2 (16 remaining Windows)
- 13 new Linux/macOS modules promised on landing page — Phase 4.3 (7 Linux) + Phase 4.4 (6 macOS), feature development not refactor
- MVVM cleanup of code-behind-heavy modules — Phase 5
- Visual regression / screenshot diff tooling — Phase 5 debt
- `StatRow` responsive narrow mode (1-column stack below 900 px) — Phase 5, bundles with AIFeaturesView narrow-mode debt

### Explicit non-goals

- **No behavior changes.** Refactor is strictly structural. Every button keeps its command, every event handler stays wired, every data path keeps the same source. Manual parity sweep enforces this.
- **No new features.** If a pilot module is missing a feature, we note it as Phase 5 or Phase 4.3+ debt and leave it missing.
- **No primitive overreach.** If a pattern appears in only 1 or 2 pilot modules, it stays inline. Third occurrence triggers a primitive extraction decision (documented in plan).

---

## 3. Vision Document alignment

This spec advances Vision Doc §10 Phase 4 by delivering the foundation. Subsequent batches (Phase 4.1–4.5) each adopt this pattern with their own smaller specs rather than re-brainstorming every layout decision.

Phase 4 roadmap extends Vision Doc §10 with a **3-wave model** agreed during brainstorming:

| Wave | Sub-phases | Scope | Est. duration |
|------|------------|-------|---------------|
| **1 — Refactor foundation** | 4.0 (this), 4.1, 4.2 | Pattern + existing 37 modules migrated | ~4 weeks |
| **2 — Feature development** | 4.3, 4.4, 4.5 | 13 new Linux/macOS modules + V1 bridge removal | ~5–6 weeks |
| **3 — Polish** | Phase 5 | Animations, narrow mode, Mica/vibrancy, known debt items | ~1 week |

Wave order is **non-negotiable**: Wave 1 before Wave 2. New modules in Wave 2 are built on the validated pattern to avoid double-refactor work.

---

## 4. Architecture — the three primitives

### 4.1 `ModuleHeader`

**Responsibility:** render a module's top bar — title + optional subtitle + optional right-aligned action cluster.

**Dependency properties:**

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `Title` | `string` | `""` | 16 px SemiBold, `TextPrimaryBrush` |
| `Subtitle` | `string?` | `null` | 11 px `TextMutedBrush`; when `null`, the subtitle `TextBlock` collapses (`IsVisible` via `StringIsNotEmptyConverter` or `x:Null` comparison) |
| `Actions` | `object?` (`[Content]` slot when a single content, otherwise wrap in `StackPanel`) | `null` | Vertically centered, right-aligned |

**Layout:** `Grid ColumnDefinitions="*,Auto"`. Left column: vertical `StackPanel` with `Title` + `Subtitle`. Right column: `ContentPresenter` bound to `Actions`. Outer margin: `0,0,0,16` (below-margin so the stack above the next card has predictable spacing).

**XAML usage:**

```xml
<controls:ModuleHeader Title="Firewall Rules"
                       Subtitle="Manage Windows Firewall inbound/outbound">
  <controls:ModuleHeader.Actions>
    <StackPanel Orientation="Horizontal" Spacing="8">
      <TextBox x:Name="SearchBox" Watermark="Search..." Width="200" />
      <Button Content="Scan Rules" Command="{Binding ScanCmd}" />
    </StackPanel>
  </controls:ModuleHeader.Actions>
</controls:ModuleHeader>
```

**Files:** `Views/Controls/ModuleHeader.axaml` + `ModuleHeader.axaml.cs` (≤ 120 lines total).

### 4.2 `StatCard`

**Responsibility:** render a single labeled metric — uppercase label + large value + accent-colored value brush.

**Dependency properties:**

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `Label` | `string` | `""` | 10 px uppercase, letter-spacing 0.5, `TextMutedBrush` |
| `Value` | `string` | `"--"` | 22–24 px Bold, color driven by `ValueBrush` |
| `ValueBrush` | `IBrush` | `{DynamicResource TextPrimaryBrush}` | Matches `StatusChip.AccentBrush` pattern for consistency |

**Layout:** `Border` with `CornerRadius="{DynamicResource RadiusMd}"`, `Background="{DynamicResource BgCardBrush}"`, `BorderBrush="{DynamicResource BorderCardBrush}"`, `BorderThickness="1"`, `Padding="12,10"`. Child: `StackPanel` vertical `Spacing="4"` with `Label` TextBlock + `Value` TextBlock.

**XAML usage:**

```xml
<controls:StatCard Label="Enabled"
                   Value="{Binding EnabledCount}"
                   ValueBrush="{DynamicResource SuccessBrush}" />
```

**Files:** `Views/Controls/StatCard.axaml` + `StatCard.axaml.cs` (≤ 100 lines total).

### 4.3 `StatRow`

**Responsibility:** container that lays StatCards side-by-side with equal width.

**Dependency properties:** none — a pure layout primitive.

**Implementation:** Inherits from `UserControl`, internal `UniformGrid Rows="1"` exposed via `[Content]` attribute so children flow directly from XAML.

**XAML usage:**

```xml
<controls:StatRow>
  <controls:StatCard Label="Total" Value="{Binding Total}" />
  <controls:StatCard Label="Enabled" Value="{Binding Enabled}"
                     ValueBrush="{DynamicResource SuccessBrush}" />
  <controls:StatCard Label="Blocked" Value="{Binding Blocked}"
                     ValueBrush="{DynamicResource ErrorBrush}" />
</controls:StatRow>
```

`UniformGrid` auto-divides based on child count (2 / 3 / 4 stats all supported). Inter-card `Margin="0,0,8,0"` handled via `StatCard` itself so empty spacing on the last card doesn't waste width — or via `UniformGrid.Padding` with inverse compensation. Plan task decides the exact approach.

**Files:** `Views/Controls/StatRow.axaml` + `StatRow.axaml.cs` (≤ 60 lines total).

### 4.4 Theme discipline

All three primitives reference **only V2 tokens**:

- Colors: `BgCardBrush`, `BorderCardBrush`, `TextPrimaryBrush`, `TextMutedBrush`, `TextSecondaryBrush`, `AccentPrimaryBrush`, `SuccessBrush`, `WarningBrush`, `ErrorBrush`
- Radii: `RadiusSm`, `RadiusMd`, `RadiusLg`
- Font sizes: `FontSizeBody`, `FontSizeBodySmall`, `FontSizeLabel`, `FontSizeHeading`

**Forbidden:**
- Raw hex literals (`#FFFFFF`, `#1a1a2e`, ...)
- `<SolidColorBrush Color="#FFFFFF" Opacity="X"/>` patterns
- Any V1 bridge key (the `AuraCoreTheme.axaml` fallbacks)

**Gap handling:** if a primitive needs a token that doesn't exist in `AuraCoreThemeV2.axaml` (e.g., `StatCardPadding`, `StatRowGap`), **add it to V2** rather than hardcoding. V1 bridge stays untouched.

### 4.5 File layout

```
src/UI/AuraCore.UI.Avalonia/Views/Controls/
├── ModuleHeader.axaml
├── ModuleHeader.axaml.cs
├── StatCard.axaml
├── StatCard.axaml.cs
├── StatRow.axaml
├── StatRow.axaml.cs
└── (existing 14 primitives unchanged)

tests/AuraCore.Tests.UI.Avalonia/Controls/
├── ModuleHeaderTests.cs
├── StatCardTests.cs
├── StatRowTests.cs
```

---

## 5. Pilot batch — the 5 migrated modules

| Order | Module | Archetype | Complexity | Why in pilot |
|-------|--------|-----------|------------|--------------|
| 1 | `DnsBenchmarkView` | Action + Result | 30 lines, simple list | Fastest validation — pattern applied to minimal module |
| 2 | `HostsEditorView` | Editor | 197 lines, TextBox + save | Editor archetype; cross-platform (/etc/hosts exists everywhere) — provides XAML cross-platform smoke |
| 3 | `AutorunManagerView` | List + Toggle | 132 lines, per-item toggle | First use of StatRow if stats present |
| 4 | `FirewallRulesView` | List + Toggle + Stats | 90 lines, 3-stat row + search | Full triple-stat + filter layout |
| 5 | `RamOptimizerView` | Action + stats + chart | 159 lines, 3 header actions + gradient bar + history graph | Pattern stress test — multiple actions, custom visualization inside GlassCard |

Ordering is **deliberate — easy → hard**. If the pattern breaks at module N, we stop and refine the primitives before continuing. `RamOptimizerView` last because its custom gradient progress bar and historical graph are the most likely to expose primitive gaps.

### 5.1 Behavior preservation rule

**Code-behind (`.axaml.cs`) is NOT refactored.** Every `x:Name` referenced by code-behind stays valid. Every event handler binding stays. Every `FindControl<T>` path stays. Only the XAML structure changes.

If an `x:Name` must move (e.g., was on a Border that becomes a StatCard), the new location keeps the same `x:Name` so the code-behind finds it without modification. The only .axaml.cs touch allowed is adding `using` imports if any new namespace is referenced by XAML — nothing more.

This preservation rule is enforced by the Seviye 3 manual parity sweep (§7.4).

### 5.2 Per-module expected size delta

| Module | Before (.axaml) | After (estimated) | Delta |
|--------|-----------------|-------------------|-------|
| `DnsBenchmarkView` | 30 | 20 | −33 % |
| `HostsEditorView` | 197 | ~110 | −44 % |
| `AutorunManagerView` | 132 | ~75 | −43 % |
| `FirewallRulesView` | 90 | ~50 | −44 % |
| `RamOptimizerView` | 159 | ~95 | −40 % |

Success criterion §9 requires every module's `.axaml` to shrink by **at least 40 %**. Shrinkage comes from replacing verbose inline `<Border>` + `<SolidColorBrush Color="#FFFFFF" Opacity="0.025"/>` patterns with 1-line primitive usage.

---

## 6. Testing strategy

### 6.1 Level 1 — Primitive unit tests

Each primitive has isolated `AvaloniaFact` tests using the Phase 1-3 pattern (`AvaloniaTestBase.RenderInWindow` + `Dispatcher.UIThread.RunJobs()` for Loaded events).

**`ModuleHeaderTests` (≥ 6 tests):**
- `Ctor_DoesNotThrow`
- `Title_AppliesToHeaderText`
- `Subtitle_AppliesToHeaderText`
- `Subtitle_Null_CollapsesTextBlock`
- `ActionsSlot_RendersProvidedContent`
- `RendersInWindow_BothColumnsVisible`

**`StatCardTests` (≥ 8 tests):**
- `Ctor_DoesNotThrow`
- `Label_RendersUppercase`
- `Value_DefaultsToPlaceholder`
- `Value_CustomBinding_Updates`
- `ValueBrush_DefaultsToTextPrimary`
- `ValueBrush_CustomBrush_AppliesToValueText` (covers Success / Warning / Error via DynamicResource)
- `RendersInWindow_HasBorderAndRadius`
- `RendersInWindow_ValueBrushReflectsProperty`

**`StatRowTests` (≥ 4 tests):**
- `Empty_RendersWithoutCrash`
- `TwoCards_SplitEqualWidth`
- `ThreeCards_SplitEqualWidth`
- `FourCards_SplitEqualWidth`

### 6.2 Level 2 — Migrated module smoke tests

Each pilot module gets a minimal regression-guard test file following the `ScheduleSectionTests` pattern (Phase 3 hotfix cb6ba23).

Per module (≥ 3 tests):
- `Ctor_DoesNotThrow` — catches theme-variant brush crashes early
- `RenderInWindow_DoesNotCrash_OnLoaded` — exercises Loaded handler including primitive binding resolution
- `ModuleHeader_HasExpectedTitle` — verifies migration applied the shell
- (when applicable) `StatRow_HasExpectedCardCount` — verifies stats migrated correctly

### 6.3 Level 3 — Manual behavior parity sweep

Automated screenshot diff tooling is **deferred to Phase 5** (see debt §11). For Phase 4.0's 5 modules, manual sweep is cheap:

Per module, the refactor author follows the §7 checklist step 6:
1. Before refactor: `dotnet run`, navigate to module, exercise features, note state
2. After refactor: same exercise, verify identical behavior
3. Any mismatch → revert XAML change, diff against prior, fix root cause (usually a wrong `x:Name` location or a missing binding)

### 6.4 Level 4 — Cross-platform XAML validation (Phase 4.1+ only)

Not in Phase 4.0 scope. Phase 4.1 inherits this spec's pattern and adds Linux/macOS visual verification when migrating the existing 8 platform-specific modules.

### 6.5 Test count targets

| Source | Tests |
|--------|-------|
| Primitive unit tests (3 primitives) | ~18 |
| Migrated module smoke tests (5 modules) | ~18 |
| Phase 3 baseline (unchanged) | 481 |
| **Phase 4.0 delivery target** | **~517 green** |

**Hard rule:** no test may fail at Phase 4.0 milestone commit. If a primitive-unit-test failure reveals a design flaw, the spec is amended before shipping (not "just skip that test").

---

## 7. Per-module refactor methodology

The following checklist is both (a) the implementation plan for Phase 4.0's 5 modules, and (b) the prompt template subsequent batches (4.1, 4.2, ...) reuse with minimal edits.

### 7.1 Step 1 — Read

- `src/UI/AuraCore.UI.Avalonia/Views/Pages/<Module>View.axaml`
- `src/UI/AuraCore.UI.Avalonia/Views/Pages/<Module>View.axaml.cs`

Note: every `x:Name`, every event handler path, every binding path.

### 7.2 Step 2 — Behavioral baseline

- `dotnet test AuraCorePro.sln --verbosity minimal` — baseline must be green
- `dotnet run` — launch app, navigate to module, exercise every button and state path
- (optional but recommended) Take screenshots of the key states

### 7.3 Step 3 — Rewrite `.axaml`

Target structure:

```xml
<UserControl ...>
  <ScrollViewer>
    <StackPanel Spacing="16">
      <controls:ModuleHeader Title="..." Subtitle="...">
        <controls:ModuleHeader.Actions>
          <!-- action buttons -->
        </controls:ModuleHeader.Actions>
      </controls:ModuleHeader>

      <!-- optional: stats row -->
      <controls:StatRow>
        <controls:StatCard Label="..." Value="{Binding ...}" />
        <!-- ... -->
      </controls:StatRow>

      <!-- primary content -->
      <controls:GlassCard>
        <!-- existing content, moved here -->
      </controls:GlassCard>

      <!-- optional: secondary content cards -->
      <controls:GlassCard>
        <!-- history / logs / advanced section -->
      </controls:GlassCard>
    </StackPanel>
  </ScrollViewer>
</UserControl>
```

Rules:
- All colors via V2 `DynamicResource` tokens
- All radii/font sizes via V2 tokens
- No raw hex, no `#FFFFFF Opacity="X"` patterns
- No V1 bridge keys
- `x:Name` values preserved exactly (code-behind still finds them)

### 7.4 Step 4 — Preserve `.axaml.cs`

**Do not edit unless strictly necessary.** Allowed changes:
- Adding `using` imports for new namespaces referenced by XAML
- Nothing else

If the refactor breaks a `FindControl<T>` because a `x:Name` moved, **move it back** in the XAML — don't rewrite the code-behind.

### 7.5 Step 5 — Write smoke tests

Create `tests/AuraCore.Tests.UI.Avalonia/Views/<Module>ViewTests.cs` with the Level 2 tests from §6.2. Run the filter:

```bash
dotnet test --filter "FullyQualifiedName~<Module>ViewTests" --verbosity minimal
```

Expected: 3–4 tests green.

### 7.6 Step 6 — Manual parity sweep

Re-launch app, re-exercise the module per §7.2 baseline. Confirm:
- Every button still works (same command fires, same state changes)
- Every data path loads (API calls, file reads, registry reads)
- Every error state looks equivalent
- Visual style uses the new shell

If anything fails: revert the XAML commit, debug, retry. The preserve-code-behind rule (§7.4) makes most failures XAML-only, quick to fix.

### 7.7 Step 7 — Commit

```
refactor(ui): migrate <Module>View to Phase 4.0 shell

- Replace hardcoded Border cards with controls:ModuleHeader + StatRow +
  GlassCard primitives
- V2 token discipline: removed all raw hex + #FFFFFF Opacity patterns
- Code-behind preserved; all x:Name, event handlers, bindings intact
- Smoke tests: ctor, render-in-window, header-title, stat-count (if
  applicable)

Before: XXX lines .axaml
After:  YYY lines .axaml (−ZZ%)

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## 8. Sequencing

Implementation is strictly serial — one module at a time, in the pilot order of §5.

### Sprint 1 — Primitives foundation

1. `ModuleHeader` + tests → commit 1
2. `StatCard` + tests → commit 2
3. `StatRow` + tests → commit 3
4. Smoke check: all 3 render in a throwaway test view without crash; full test suite green (481 + ~18 = 499)

### Sprint 2 — Pilot migrations (sequential)

Per §5.1 ordering:

5. `DnsBenchmarkView` refactor + smoke → commit 4
6. `HostsEditorView` refactor + smoke → commit 5
7. `AutorunManagerView` refactor + smoke → commit 6
8. `FirewallRulesView` refactor + smoke → commit 7
9. `RamOptimizerView` refactor + smoke → commit 8

After each module: full test suite run. If anything beyond the new smoke tests fails, stop, diagnose, fix before continuing.

### Sprint 3 — Validation + milestone

10. Smoke tests for all 5 in a single aggregated commit (if not already split per-module) → commit 9
11. Phase 4.0 milestone commit → commit 10

**Total commits:** ~10 atomic commits on `phase-4-module-pattern` branch.

### Risk mitigation by ordering

- `RamOptimizerView` last: if its custom gradient progress bar + history graph doesn't fit the pattern, Sprint 2 pauses, the primitives are refined, and we proceed only after the refined primitives pass all tests of modules 1–4. Worst case: RamOptimizer gets moved to Phase 4.1 and a `GradientProgressBar` primitive is added then.

---

## 9. Success criteria

All boxes checked at milestone commit:

- [ ] 3 primitives delivered (`ModuleHeader`, `StatCard`, `StatRow`), each under their line budget (§4.1–4.3)
- [ ] ~18 primitive unit tests green
- [ ] 5 pilot modules migrated
- [ ] ~18 module smoke tests green
- [ ] Full solution 481 → ~517 tests green, zero regressions
- [ ] Each pilot `.axaml` shrunk by ≥ 40 %
- [ ] Each pilot `.axaml.cs` unchanged except optional `using` additions
- [ ] Manual parity sweep: each pilot module exercised before + after, behavior identical
- [ ] ~10 atomic commits on `phase-4-module-pattern` branch
- [ ] Milestone commit with the Phase 4.0 summary

---

## 10. Phase 4 roadmap (forward-reference)

Phase 4.0 is the first of six sub-phases. Subsequent specs reuse this spec's pattern and methodology, scoping only the batch-specific module list + any primitive additions needed for that batch.

| Sub-phase | Scope | Primitives needed | Duration |
|-----------|-------|-------------------|----------|
| **4.0** (this) | Pattern + 3 primitives + 5 Windows pilot | ModuleHeader / StatCard / StatRow | ~1 week |
| **4.1** | Migrate 8 existing Linux/macOS modules | (possibly `GradientProgressBar` if pulled from RamOptimizer) | ~1 week |
| **4.2** | Migrate ~16 remaining Windows modules (2 batches of 7–8) | As needed per batch | ~2 weeks |
| **4.3** | Feature development: 7 new Linux modules (Journal Cleaner, Kernel Cleaner, Linux App Installer, Snap/Flatpak Cleaner, GRUB Manager, Docker Cleaner, +1) | Variant B (Hero) or C (Toolbar) as needed | ~3 weeks |
| **4.4** | Feature development: 6 new macOS modules (Xcode Cleaner, DNS Flusher, Purgeable Space, Spotlight Manager, Mac App Installer, +1) | As needed | ~2 weeks |
| **4.5** | Remove V1 theme bridge (`AuraCoreTheme.axaml`); final cleanup | — | ~3 days |

Each sub-phase gets its own brainstorm → spec → plan → implementation cycle. Phase 4.1's spec will note cross-platform visual verification requirements (Linux/macOS VM testing).

---

## 11. Known debt carried forward (no fix in Phase 4.0)

These items stay on the Phase 5 / "future" debt list — this spec explicitly does NOT address them:

1. **V1 theme bridge (`AuraCoreTheme.axaml`)** — kept active for 32 un-migrated modules. Removal in Phase 4.5.
2. **StatRow responsive narrow mode (< 900 px)** — single-column stack layout. Phase 5 together with AIFeaturesView narrow mode.
3. **Visual regression / screenshot diff tooling** — manual parity sweep suffices for 5 modules; automate when batch sizes justify 1–2 days of infra work (likely Phase 4.2 or Phase 5).
4. **MVVM refactor of code-behind-heavy modules** — Phase 4 migration preserves code-behind as-is; MVVM cleanup is Phase 5.
5. **`IAuraCoreLLM.ReloadAsync`** — Phase 3 debt, not Phase 4's concern.
6. **AIFeaturesView "Overview" back-nav discoverability** — Phase 5.
7. **ChatOptInDialog Step 2 UX polish** — Phase 5.
8. **CORTEX status hardcoded English** — Phase 5.
9. **AppSettings thread safety + `INotifyPropertyChanged`** — Phase 5.
10. **Smart Optimize CTA deep-link to Recommendations section** — Phase 5.
11. **HSTS preload directive cleanup** — Phase 5 or ops-time.
12. **UserSession → SidebarViewModel tier integration** — Phase 5.

---

## 12. Rollback strategy

If Phase 4.0 delivery fails review (e.g., pattern doesn't validate on RamOptimizer):

- Each commit is atomic. Safe revert points:
  - Revert module migrations individually (commits 4–8) keeps the 3 primitives built for re-use
  - Revert the whole primitives + migrations stack (commits 1–8) returns to `phase-3-ai-features` HEAD `7045d5a`
- Branch `phase-4-module-pattern` lives on until review closes; no merge to `phase-3-ai-features` or `main` without explicit approval
- Worst case: Phase 4.0 is redesigned in a fresh brainstorm session using lessons learned, and `phase-4-module-pattern` is deleted

---

## 13. Open questions deferred to implementation

Minor decisions punted to the implementation plan so brainstorm stays scoped:

1. Exact spacing for `ModuleHeader` bottom margin — 16 px or a new `ModuleHeaderGap` V2 token
2. `StatRow` inter-card spacing mechanism — `StatCard` right margin (simpler but wastes width on last card) vs `UniformGrid` internal padding (cleaner but bespoke)
3. `ModuleHeader.Subtitle` collapse mechanism — `StringIsNotEmptyConverter` (reusable) vs `x:Null` comparison (minimal)
4. Whether `StatRow` should be a `UserControl` subclass or a `ContentControl` with custom template — both work; implementation chooses simpler

These are implementation-phase decisions, not design-phase. None affect the overall architecture.

---

## 14. Sign-off

Design approved by human partner on 2026-04-16 after 5-section brainstorm. Ready for `superpowers:writing-plans` skill to produce step-by-step implementation plan.
