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

## Verification

Three consecutive `dotnet test AuraCore.Tests.UI.Avalonia` runs after B2 landed (commit `46cc163`) — all clean:

```
=== run 1 ===
Passed!  - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555
=== run 2 ===
Passed!  - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555
=== run 3 ===
Passed!  - Failed: 0, Passed: 1555, Skipped: 0, Total: 1555
```

No flake observed. Previously-intermittent tests (`ServiceManagerViewTests.Layout_UsesModuleHeader`, `DashboardView` render tests, etc.) are now deterministic.

## Residual risk (future-proof)

- If a new view is added that calls `App.Services.GetRequiredService<NewType>()` and `NewType` isn't registered in `AvaloniaTestApplication.Initialize()`, tests constructing that view will fail with `InvalidOperationException` — NOT a flake (deterministic, easy to diagnose). Fix: register `NewType` in the test harness DI container.
- xUnit parallel execution still has theoretical shared-state risks (e.g., `LocalizationService.CurrentLanguage` mutation). Not observed as a flake today but could surface if a future test sets language in setup without resetting in teardown. If that becomes an issue, adding `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to the test project's AssemblyInfo is the standard mitigation.

## Hypotheses ruled out during investigation

- **LocalizationService shared state**: no tests in the current suite mutate `CurrentLanguage` without resetting. Not the cause.
- **Dispatcher.UIThread uncompleted work**: checked; no test asserts immediately after a `Dispatcher.UIThread.Post` without awaiting.
- **xUnit parallelism**: possibly a contributing factor to the non-determinism, but the underlying null `App.Services` was the primary cause. Parallelism amplified the observability but wasn't the root.

## Conclusion

**B1 requires no standalone code change.** The flake was a symptom of the missing test-harness DI bootstrap, which B2 addressed. This document records the investigation for future reference in case similar symptoms reappear.
