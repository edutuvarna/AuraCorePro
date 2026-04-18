// Phase debt-B1 — UI test flake hardening.
//
// Avalonia headless tests share static state (pipelines, DataContext, resources)
// that can race under xUnit parallel execution, producing intermittent failures.
// Disabling parallelism at the assembly level makes this suite deterministic.
//
// Cost: slightly slower suite wall time (~20s vs ~15s on this machine). Worth it
// for eliminating the flake.

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
