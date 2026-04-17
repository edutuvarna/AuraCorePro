# UI Test Flake Investigation (Task B1)

**Date:** 2026-04-17
**Outcome:** Root cause identified; implicitly fixed by Task B2's DI bootstrap. No additional code change required for B1.

---

## Symptom

Every `dotnet test AuraCorePro.sln` run had 0–4 random UI test failures on the first pass; second pass clean. The flake worsened over time as more views were added that depend on DI-resolved services.

Representative symptom (seen during Phase 5.5 + debt sweep baselines):
```
Failed!  - Failed:     1, Passed:  1475, Skipped:     6, Total:  1482, Duration: 19 s
```
with the failure typically being `ServiceManagerViewTests.Layout_UsesModuleHeader` or similar view-construction tests.

## Root cause

`App.Services` (the static `IServiceProvider` on `AuraCore.UI.Avalonia.App`) was **not initialized** in the headless Avalonia test harness before Task B2. The production code path populates it in `App.OnFrameworkInitializationCompleted`, but the test harness (`AvaloniaTestApplication`) only called `Initialize()` which set up styles + converters, skipping DI.

Several views call `App.Services.GetService<T>()` (or `GetRequiredService<T>()`) during their `InitializeComponent` / `Loaded` events. With `App.Services` null:

- Some tests completed before any view tried to resolve a service → passed.
- Tests that happened to run after the harness had been running long enough that something else populated `App.Services` (unlikely but possible under xUnit parallelism) → passed.
- Tests that ran when `App.Services` was null AND tried to resolve a service → NullReferenceException inside the view constructor → failed.

This race was non-deterministic because:
1. xUnit parallel test execution interleaved test ordering.
2. Some tests constructed full view trees; others constructed partial or VM-only shells, touching `App.Services` at different times.
3. The headless harness's static state carried over between tests run in the same assembly load.

## Why B2 fixed it

Task B2 added a minimal DI bootstrap to `AvaloniaTestApplication.Initialize()`:

```csharp
var services = new ServiceCollection();
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
services.AddSingleton<IOptimizationModule, SystemHealthModule>();
services.AddSingleton<IOptimizationModule, BloatwareRemovalModule>();
services.AddSingleton<IOptimizationModule, RamOptimizerModule>();
App.Services = services.BuildServiceProvider();
```

Now `App.Services` is always non-null + populated with a known set of services before ANY test constructs a view. Calls to `GetService<T>()` consistently return either the registered instance or `null` (for unregistered types), eliminating the null-reference path.

## Verification — initial B2 result was incomplete

Three consecutive `dotnet test AuraCore.Tests.UI.Avalonia` runs immediately after B2 landed (commit `46cc163`) — all clean. This looked like a complete fix.

**However**, the next full-solution run (at Sub-wave B milestone commit) surfaced `Failed! - Failed: 1, Passed: 1554` on UI.Avalonia again. One more investigation pass established that B2 alone was necessary but not sufficient: it eliminated the null-`App.Services` null-reference failures, but a secondary race under xUnit parallelism remained. Multiple tests that mutate shared static state (Avalonia resources, `LocalizationService`, DataContext caches) could still interleave non-deterministically.

## The full fix — parallelism disable

Added `tests/AuraCore.Tests.UI.Avalonia/AssemblyInfo.cs` with:

```csharp
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
```

This serializes test execution within the `AuraCore.Tests.UI.Avalonia` assembly. Combined with B2's DI bootstrap, the harness is now deterministic.

**Verification — 5 consecutive clean runs after parallelism disable:**

```
=== run 1 === Passed! - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555, Duration: 27s
=== run 2 === Passed! - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555, Duration: 27s
=== run 3 === Passed! - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555, Duration: 27s
=== run 4 === Passed! - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555, Duration: 27s
=== run 5 === Passed! - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555, Duration: 28s
```

No flake observed. Previously-intermittent tests (`ServiceManagerViewTests.Layout_UsesModuleHeader`, `DashboardView` render tests, etc.) are now deterministic.

**Cost:** suite wall time went from ~15-21s (parallel) to ~27s (serial). ~+50% for this one assembly. Acceptable trade for zero flake.

## Residual risk (future-proof)

- If a new view is added that calls `App.Services.GetRequiredService<NewType>()` and `NewType` isn't registered in `AvaloniaTestApplication.Initialize()`, tests constructing that view will fail with `InvalidOperationException` — NOT a flake (deterministic, easy to diagnose). Fix: register `NewType` in the test harness DI container.
- The parallelism-disable flag only applies within this assembly. If UI tests are ever moved to another test project, the flag must move with them.
- Individual test methods can still technically race against themselves via async fire-and-forget in Avalonia view event handlers, but this hasn't been observed in practice.

## Two root causes, two fixes

| Root cause | Impact | Fix |
|---|---|---|
| `App.Services` null in test harness | NullReferenceException during view construction | B2's minimal DI bootstrap in `AvaloniaTestApplication.Initialize()` |
| Shared-state race under xUnit parallel execution | Non-deterministic failures even with DI working | `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `tests/AuraCore.Tests.UI.Avalonia/AssemblyInfo.cs` |

Both together produce a deterministic, all-green suite.

## Conclusion

B1 required **two code changes** across both B2 (DI bootstrap, commit `46cc163`) and a follow-up commit (parallelism disable). Both are small and well-scoped. The flake is now gone as verified by 5 consecutive clean runs post-fix.
