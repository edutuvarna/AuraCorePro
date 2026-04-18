# Handoff — Phase 6 Item 2 Execution (2026-04-18)

**For next session — read this first.** Brainstorm + spec + plan all committed; execution is pure subagent-driven from Task 1.

## Current state

- **Branch:** `phase-6-pixel-regression` (new, 2 commits ahead of main)
- **HEAD:** `c1fad3a` (plan commit on top of `fef47c5` spec commit)
- **Base:** `main` HEAD `64c56ef` (Phase 6.1 merge — pushed to origin)
- **Tests:** 2250 passing + 0 skipped + 0 failed across 8 assemblies
- **Baseline for Phase 6.2:** 2250 → expected ~2280 at close (+30 pixel tests)

## Bootstrap (first commands)

```bash
cd C:/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git checkout phase-6-pixel-regression
git log --oneline -5                        # confirm HEAD c1fad3a
dotnet test AuraCorePro.sln --verbosity minimal --nologo 2>&1 | grep -E "^Passed!"
# Expected: 8 Passed! lines, ~2250 total
```

## Read-before-starting

Order:

1. `docs/superpowers/plans/2026-04-18-phase6.2-pixel-regression.md` — full 13-task plan with code for every step
2. `docs/superpowers/specs/2026-04-18-phase6.2-pixel-regression-design.md` — design rationale if plan isn't self-evident
3. `C:/Users/Admin/.claude/projects/C--/memory/project_phase_6_roadmap.md` — broader Phase 6 roadmap (Item 2 is #2 of 6)
4. `C:/Users/Admin/.claude/projects/C--/memory/project_phase_6_item_1_deeplink_url_complete.md` — immediate predecessor memory (sets expectations for workflow discipline)

## Scope at a glance

Phase 6 Item 2 = **Pixel-Regression Testing Infrastructure**. Scope B (Standard):
- Verify.Xunit + Verify.ImageSharp (2 new NuGets)
- `PixelRegressionHarness` that renders Avalonia views to PNG bytes
- 15 views × 2 sizes = 30 goldens in-repo at `tests/AuraCore.Tests.UI.Avalonia/goldens/`
- Accept-new-goldens workflow documented
- `.gitignore` rule for `.received.png` artifacts

4 sub-waves: 6.2.A framework + PoC → 6.2.B core shell (5 views) → 6.2.C modules (10 views) → 6.2.D docs.

## Known risks / judgment calls

### Task 3: Avalonia headless frame-capture API

The exact API for capturing a rendered Avalonia window as a bitmap is **version-dependent** and not 100% locked in the plan. The plan gives one primary attempt (`window.CaptureRenderedFrame()` via `Avalonia.Headless`) and documents fallbacks (`RenderTargetBitmap.Render(view)` + `Bitmap.Save(stream)`).

**Guidance for the subagent implementer:** look at existing rendering patterns first:

```bash
grep -rn "HeadlessWindow\|CaptureRenderedFrame\|RenderTargetBitmap\|PngStream" \
     tests/AuraCore.Tests.UI.Avalonia/ --include="*.cs" 2>/dev/null
grep -rn "Avalonia.Headless" src/ --include="*.cs" 2>/dev/null | head -5
```

If neither `CaptureRenderedFrame` nor `RenderTargetBitmap` work cleanly, check Avalonia version in `Directory.Packages.props` or `AuraCore.UI.Avalonia.csproj` and look up the headless docs for that specific version. Avalonia 11.x generally supports `WriteableBitmap`-based render-to-memory; Avalonia 0.10.x used a different API. Document whichever works in the commit message.

### DI-dependent views

Task 6 + Task 8 will encounter views whose constructors call `App.Services.GetService<T>()`. The `AvaloniaTestApplication.Initialize()` DI bootstrap (from Phase 5 debt sweep Task B2) registers 3 pilot modules; new modules added in Phase 5.5 (Driver Updater, Defender Manager, Service Manager) are NOT in there yet.

When Task 8 fires exceptions instead of pixel diffs, the subagent should:
1. Check exception message for missing service type
2. Register it in `AvaloniaTestApplication.Initialize()` (probably as the module singleton)
3. Re-run until all 20 `.received.png` files appear (no exceptions)
4. THEN do visual inspection + accept

### Golden acceptance requires human judgment

The plan explicitly tells the subagent to "visually inspect the `.received.png`" before accepting. If this runs fully autonomous under AFK mode, the subagent can't actually eyeball a PNG. **For the first golden-accept (Task 4), the operator (you OR the controller) should inspect the PoC Dashboard `.received.png` manually** to confirm the harness is producing sane output. After that, subsequent goldens can be accepted with a quick sanity check (non-blank, non-crash).

If AFK-mode executes without human inspection, there's a small risk of committing a corrupt golden. Mitigation: keep the session interactive for at least Task 4 (the first golden), then AFK for the rest.

## Execution flow recommendation

**Interactive for Task 1-5** (framework install + PoC + first-golden visual check + 6.2.A milestone). This is the riskiest stretch — verify the harness actually produces sensible PNGs before mass-producing.

**AFK-safe for Task 6-12** (core shell + modules + docs + milestones). Harness is proven by Task 4; remaining work is templated expansion.

**Interactive for Task 13** (ceremonial close — memory + MEMORY.md edits benefit from human eyes).

## First moves for the new session

```
1. cd + checkout + baseline test (see Bootstrap above)
2. Read the plan (13 tasks, ~1000 lines)
3. Invoke superpowers:subagent-driven-development
4. Start with Task 1 (haiku — add Verify packages to csproj)
5. Monitor closely through Task 4 (PoC golden — needs manual PNG inspection)
6. After Task 4 proves harness, continue through milestones
```

## Phase 6 roadmap position

Per `project_phase_6_roadmap.md`:

1. ✅ Item 1 Deep-link URL routing (merged to main as `64c56ef`)
2. ⏳ **Item 2 Pixel-regression testing infra** — this session
3. ⏸️ Item 3 Light theme
4. ⏸️ Item 4 TR completion sweep (RE-SCOPED — not new language)
5. ⏸️ Item 5 Mobile/tablet responsiveness
6. ⏸️ Item 6 macOS notarization (user provides Mac hw)

## Active behavior preferences (unchanged)

- `feedback_subagent_driven_default.md` — default to subagent-driven, don't ask
- `feedback_skip_spec_review.md` — no spec-review user-gate
- `feedback_supervisor_mode.md` — main-session inline verification post-subagent
- `feedback_gpu_cpu_limit.md` — 80-85% max GPU/CPU
- `feedback_notify_before_gpu.md` — ask before GPU-heavy work
- `feedback_visual_companion_always_yes.md` — skip consent prompt if offered
- `feedback_afk_default_recommended.md` — **dormant** unless user re-declares AFK

## Session context notes

- Current session spent ~68% context on Phase 6 Item 1 end-to-end shipment (spec + plan + 18 tasks + merge + push + Item 2 brainstorm + spec + plan) — heavy, but clean break point now.
- New session starts fresh (plan loading ~10% context). Expect execution to run ~50% of context budget — plenty of headroom.

---

**You're set.** Next session should hit the ground sprinting. 🚀
