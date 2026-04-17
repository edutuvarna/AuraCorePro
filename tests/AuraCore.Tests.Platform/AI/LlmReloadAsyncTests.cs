using System.ComponentModel;
using AuraCore.Application.Interfaces.Engines;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.AI;

/// <summary>
/// Tests for <see cref="IAuraCoreLLM.ReloadAsync"/> + <see cref="IAuraCoreLLM.IsReloading"/> INPC.
///
/// The behavioural tests use <see cref="FakeLlm"/> — a hand-rolled test double
/// that implements the full contract — to avoid pulling the heavyweight
/// LLamaSharp / ML.NET stack into the Platform test project.
/// The interface-shape tests use reflection on the <see cref="IAuraCoreLLM"/> type,
/// which is already reachable via the AuraCore.Application project reference.
/// </summary>
public class LlmReloadAsyncTests
{
    // ─── Interface-shape assertions (reflection; always run) ─────────────────

    [Fact]
    public void ReloadAsync_is_declared_on_IAuraCoreLLM_interface()
    {
        var method = typeof(IAuraCoreLLM).GetMethod("ReloadAsync");
        method.Should().NotBeNull("ReloadAsync must be declared on IAuraCoreLLM");

        var paramList = method!.GetParameters();
        paramList.Should().HaveCount(2, "ReloadAsync must accept (LlmConfiguration? newConfig, CancellationToken ct)");

        paramList[0].ParameterType.Should().Be(typeof(LlmConfiguration),
            "first parameter must be LlmConfiguration?");
        paramList[1].ParameterType.Should().Be(typeof(CancellationToken),
            "second parameter must be CancellationToken");
    }

    [Fact]
    public void IsReloading_is_declared_on_IAuraCoreLLM_interface()
    {
        var prop = typeof(IAuraCoreLLM).GetProperty("IsReloading");
        prop.Should().NotBeNull("IsReloading must be declared on IAuraCoreLLM");
        prop!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void IAuraCoreLLM_extends_INotifyPropertyChanged()
    {
        typeof(IAuraCoreLLM).IsAssignableTo(typeof(INotifyPropertyChanged))
            .Should().BeTrue("IAuraCoreLLM must extend INotifyPropertyChanged for UI binding");
    }

    // ─── Behavioural tests (FakeLlm test double) ──────────────────────────────

    [Fact]
    public async Task ReloadAsync_null_config_completes_without_throwing()
    {
        var llm = new FakeLlm();

        Func<Task> act = () => llm.ReloadAsync(newConfig: null);
        await act.Should().NotThrowAsync();
        llm.IsReloading.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadAsync_raises_IsReloading_INPC_true_then_false()
    {
        var llm = new FakeLlm();
        var changes = new List<(string? PropName, bool CurrentValue)>();

        ((INotifyPropertyChanged)llm).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IAuraCoreLLM.IsReloading))
                changes.Add((e.PropertyName, llm.IsReloading));
        };

        await llm.ReloadAsync();

        // Must flip true then false — 2 INPC raises minimum
        changes.Count.Should().BeGreaterOrEqualTo(2,
            "IsReloading must fire INPC when set to true and again when reset to false");
        changes.Should().Contain(c => c.CurrentValue == true,
            "IsReloading must transition to true during reload");
        changes.Last().CurrentValue.Should().BeFalse(
            "IsReloading must return to false after reload completes");
    }

    [Fact]
    public async Task ReloadAsync_concurrent_call_throws_InvalidOperation()
    {
        // Use a TCS-gated fake so the first reload is deterministically held open
        // while we fire the second call — no timing dependency.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var llm = new FakeLlm(blockUntil: gate.Task);

        // Start first reload — it will wait on gate before completing
        var first = llm.ReloadAsync();

        // Yield to let the first reload enter its async body and acquire the semaphore
        await Task.Yield();

        Func<Task> second = () => llm.ReloadAsync();
        await second.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already reloading*");

        // Release the first reload
        gate.SetResult(true);
        await first;
    }

    [Fact]
    public async Task ReloadAsync_respects_cancellation_token()
    {
        // Use a TCS-gated fake so cancellation is deterministic
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var llm = new FakeLlm(blockUntil: gate.Task);
        using var cts = new CancellationTokenSource();

        var reloadTask = llm.ReloadAsync(newConfig: null, cts.Token);

        // Yield to let the reload enter its async body, then cancel
        await Task.Yield();
        cts.Cancel();

        Func<Task> act = () => reloadTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
        llm.IsReloading.Should().BeFalse("cancellation must reset IsReloading to false");

        // Let the gate go (already cancelled — just cleanup)
        gate.SetResult(true);
    }

    [Fact]
    public async Task ReloadAsync_precancelled_throws_and_IsReloading_stays_false()
    {
        var llm = new FakeLlm();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel

        Func<Task> act = () => llm.ReloadAsync(newConfig: null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        llm.IsReloading.Should().BeFalse("cancellation must reset IsReloading to false");
    }

    [Fact]
    public async Task ReloadAsync_with_new_config_swaps_config()
    {
        var llm = new FakeLlm();
        var newConfig = new LlmConfiguration(ModelPath: "/models/new-model.gguf");

        await llm.ReloadAsync(newConfig);

        llm.IsReloading.Should().BeFalse();
        llm.LastAppliedConfig.Should().Be(newConfig,
            "ReloadAsync must record the new config when one is supplied");
    }

    [Fact]
    public async Task ReloadAsync_null_config_preserves_existing_config()
    {
        var initial = new LlmConfiguration(ModelPath: "/models/original.gguf");
        var llm = new FakeLlm(initialConfig: initial);

        await llm.ReloadAsync(newConfig: null);

        llm.LastAppliedConfig.Should().Be(initial,
            "null newConfig must reload the current config, not replace it");
    }
}

// ─── FakeLlm test double ──────────────────────────────────────────────────────

/// <summary>
/// In-memory implementation of <see cref="IAuraCoreLLM"/> for unit tests.
/// Supports configurable reload delay for concurrency / cancellation testing.
/// </summary>
file sealed class FakeLlm : IAuraCoreLLM
{
    private readonly TimeSpan _reloadDelay;
    private readonly Task? _blockUntil;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private bool _isReloading;
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <param name="reloadDelay">Fixed delay simulating slow reload.</param>
    /// <param name="blockUntil">When set, ReloadAsync waits for this Task before completing (for deterministic concurrency tests).</param>
    /// <param name="initialConfig">Starting config value.</param>
    public FakeLlm(
        TimeSpan reloadDelay = default,
        Task? blockUntil = null,
        LlmConfiguration? initialConfig = null)
    {
        _reloadDelay = reloadDelay == default ? TimeSpan.Zero : reloadDelay;
        _blockUntil = blockUntil;
        LastAppliedConfig = initialConfig;
    }

    public bool IsAvailable => true;

    public LlmConfiguration? LastAppliedConfig { get; private set; }

    public bool IsReloading
    {
        get => _isReloading;
        private set
        {
            if (_isReloading == value) return;
            _isReloading = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReloading)));
        }
    }

    public Task<string> AskAsync(string question, LlmContext? context, CancellationToken ct = default)
        => Task.FromResult("fake response");

    public async Task ReloadAsync(LlmConfiguration? newConfig = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!await _reloadGate.WaitAsync(0, CancellationToken.None))
            throw new InvalidOperationException("IAuraCoreLLM.ReloadAsync: already reloading");

        try
        {
            IsReloading = true;
            ct.ThrowIfCancellationRequested();

            if (_reloadDelay > TimeSpan.Zero)
                await Task.Delay(_reloadDelay, ct);

            // If a deterministic block gate is provided, wait for it (used in concurrency tests)
            if (_blockUntil is not null)
                await _blockUntil.WaitAsync(ct);

            ct.ThrowIfCancellationRequested();

            // Apply config (null = keep existing)
            if (newConfig is not null)
                LastAppliedConfig = newConfig;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            IsReloading = false;
            _reloadGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reloadGate.Dispose();
    }
}
