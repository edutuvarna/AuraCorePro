# Pixel-Regression Goldens

This directory holds `.verified.png` + `.verified.txt` "golden" snapshots
compared against on every test run by Verify.Xunit + Verify.ImageSharp.

Covered by Phase 6.2 (see `docs/superpowers/specs/2026-04-18-phase6.2-pixel-regression-design.md`
for the design rationale, and the memory file
`project_phase_6_item_2_pixel_regression_complete.md` for the close-out summary).

## Scope

**What these tests catch:** regressions in the design-system primitives —
token changes (colors, radii, spacing, typography), `StatusChip` /
`AccentBadge` control changes, and the tinted semantic status surfaces.

**What these tests deliberately don't cover:** full-page views
(`DashboardView`, `AIFeaturesView`, module pages). Those embed OS-dependent
live data directly in their code-behind (`Environment.TickCount64`,
`PerformanceCounter`, WMI, `DispatcherTimer` polling, async disk scans)
with no clean DI seam to swap for deterministic test doubles. Snapshots of
those drift run-to-run — uptime advances, async data fills, GPU sampler
wakes — so they'd fail on every commit without a per-view refactor that's
out of Phase 6.2 scope.

The pivot mirrors industry practice (Storybook, Percy): snapshot at the
component layer, not the page layer.

## Render target

Tests render views under `Avalonia.Headless` with `UseSkia()` +
`UseHeadlessDrawing=false` so real pixels come out. Config lives in
`tests/AuraCore.Tests.UI.Avalonia/AvaloniaTestAssembly.cs`. The
`AuraCoreThemeV2.axaml` + `Icons.axaml` style dictionaries are loaded in
`AvaloniaTestApplication.Initialize()` under `RequestedThemeVariant=Dark`
so design-token references resolve.

Capture path: `PixelRegressionHarness.RenderViewAsync<TView>(w, h)` →
`Window.Show` → `Measure` + `Arrange` → `Dispatcher.UIThread.RunJobs()` →
`window.CaptureRenderedFrame()` → `WriteableBitmap.Save(MemoryStream)` →
PNG bytes.

Verify-side the `PixelVerify.Verify(png)` helper chains
`.UseDirectory("../goldens")` so snapshots land in this folder.

## How to accept new goldens (after an intentional UI change)

When a UI change causes a pixel test to fail, Verify writes a
`.received.png` alongside the old `.verified.png`. To accept, promote
`.received.*` → `.verified.*`.

### Option 1: Verify CLI tool (recommended)

Install once per machine:

```
dotnet tool install -g Verify.Tool
```

Accept all pending changes in this project:

```
cd tests/AuraCore.Tests.UI.Avalonia
verify accept-all
```

Accept a single test's pending change:

```
verify accept DesignSystemGalleryPixelTests.Gallery_wide
```

### Option 2: Manual rename

```
cd tests/AuraCore.Tests.UI.Avalonia/goldens
mv DesignSystemGalleryPixelTests.Gallery_wide.received.png  DesignSystemGalleryPixelTests.Gallery_wide.verified.png
mv DesignSystemGalleryPixelTests.Gallery_wide.received.txt  DesignSystemGalleryPixelTests.Gallery_wide.verified.txt
```

Each failing test produces both a `.png` and a `.txt` metadata file — promote both.

### Option 3: Per-run auto (dev-only, NEVER commit to CI)

```
# Bash
VERIFY_AutoVerify=true dotnet test

# PowerShell
$env:VERIFY_AutoVerify = "true"; dotnet test
```

This masks regressions — only use for the one session where you know the
entire diff is intentional, then unset before committing.

## Accept vs. fix code

**Accept** if the `.received.png` shows:
- The token / style change you just made (intentional)
- New primitives you added to the gallery

**Fix code** if the `.received.png` shows:
- Black / blank canvas (render pipeline broken)
- Missing sections (view construction threw)
- Wildly-off colors (theme loading broke)
- Random artifacts (GPU / driver quirk — retry a second time first)

## Adding a new primitive to gallery coverage

1. Edit `tests/AuraCore.Tests.UI.Avalonia/TestViews/DesignSystemGallery.axaml` —
   add the primitive to the appropriate section, or add a new `<Border>` card
   with a new label. Use only design-token references, never live bindings
   or DI lookups.
2. Run `dotnet test --filter "FullyQualifiedName~DesignSystemGalleryPixelTests"` —
   both `Gallery_wide` and `Gallery_narrow` fail with a pixel diff.
3. Visually inspect the new `.received.png` files — confirm the new
   primitive renders correctly.
4. Accept via Option 1 or 2 above.
5. Commit the `.axaml` edit + the two updated `.verified.png` files.

## Adding a new pixel test for a separate surface

1. Decide whether it's presentational enough — constructor must not call
   `App.Services.GetService`, must not start timers, must not read OS state.
   If it does, snapshot regressions will drift; either mock the seam or
   skip it.
2. Add a test class in
   `tests/AuraCore.Tests.UI.Avalonia/PixelRegression/` tagged
   `[Trait("Category", "PixelRegression")]`.
3. Methods use `[AvaloniaFact]` + `PixelRegressionHarness.RenderViewAsync<T>` +
   `PixelVerify.Verify(png)`.
4. Run, inspect `.received.png`, accept as above.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Test throws `NotSupportedException: use Skia + UseHeadlessDrawing=false` | Harness config not applied | Check `AvaloniaTestAssembly.cs` — should call `.UseSkia()` and `UseHeadlessDrawing=false` |
| Test throws exception instead of producing a diff | View constructor has unsatisfied DI dependency or calls OS APIs | Either register the service in `AvaloniaTestApplication.Initialize()`, or reconsider whether this view belongs in pixel coverage (live-data views are deliberately out of scope) |
| `.received.png` is blank / all-black | Theme dictionaries didn't load | Confirm `AuraCoreThemeV2.axaml` + `Icons.axaml` StyleIncludes still present in `AvaloniaTestApplication.Initialize()` |
| All tests fail with pixel diff after pulling main | Someone updated tokens or the gallery without pushing accepted goldens | Pull again after they push, or re-run their accept workflow locally |
| Uptime / time-relative text visible in a snapshot | Caller added a live-data view to coverage — it'll flake | Move back to the gallery, or build a test double for the data source |
