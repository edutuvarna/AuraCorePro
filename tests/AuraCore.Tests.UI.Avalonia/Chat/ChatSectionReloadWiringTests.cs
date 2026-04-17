using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.UI.Avalonia.Views.Pages.AI;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Chat;

public class ChatSectionReloadWiringTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeLlm : IAuraCoreLLM
    {
        private readonly Func<LlmConfiguration?, CancellationToken, Task> _onReload;

        public FakeLlm(Func<LlmConfiguration?, CancellationToken, Task> onReload)
        {
            _onReload = onReload;
        }

        public bool IsAvailable => true;
        public bool IsReloading { get; private set; }

        public Task<string> AskAsync(string question, LlmContext? context, CancellationToken ct = default)
            => Task.FromResult("stub");

        public Task ReloadAsync(LlmConfiguration? newConfig = null, CancellationToken ct = default)
            => _onReload(newConfig, ct);

        public void Dispose() { }

        // INotifyPropertyChanged — unused in these unit tests
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyModelChangeAsync_routes_reload_to_llm()
    {
        LlmConfiguration? captured = null;
        var llm = new FakeLlm((cfg, _) =>
        {
            captured = cfg;
            return Task.CompletedTask;
        });

        var config = new LlmConfiguration("models/qwen25-32b.gguf");
        var result = await ChatSection.ApplyModelChangeAsync(llm, config, CancellationToken.None);

        Assert.Equal(ChatSection.ReloadStatus.Ok, result.Status);
        Assert.NotNull(captured);
        Assert.Equal("models/qwen25-32b.gguf", captured!.ModelPath);
    }

    [Fact]
    public async Task ApplyModelChangeAsync_on_OperationCanceled_returns_Cancelled()
    {
        var llm = new FakeLlm((_, _) => throw new OperationCanceledException());

        var result = await ChatSection.ApplyModelChangeAsync(
            llm,
            new LlmConfiguration("m.gguf"),
            CancellationToken.None);

        Assert.Equal(ChatSection.ReloadStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task ApplyModelChangeAsync_on_InvalidOperationException_returns_Busy()
    {
        var llm = new FakeLlm((_, _) => throw new InvalidOperationException("already reloading"));

        var result = await ChatSection.ApplyModelChangeAsync(
            llm,
            new LlmConfiguration("m.gguf"),
            CancellationToken.None);

        Assert.Equal(ChatSection.ReloadStatus.Busy, result.Status);
    }

    [Fact]
    public async Task ApplyModelChangeAsync_on_generic_exception_returns_Failed()
    {
        var llm = new FakeLlm((_, _) => throw new Exception("boom"));

        var result = await ChatSection.ApplyModelChangeAsync(
            llm,
            new LlmConfiguration("m.gguf"),
            CancellationToken.None);

        Assert.Equal(ChatSection.ReloadStatus.Failed, result.Status);
        Assert.Contains("boom", result.Error ?? "");
    }
}
