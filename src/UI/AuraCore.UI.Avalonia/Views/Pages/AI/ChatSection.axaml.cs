using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application.Interfaces.Engines;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages.AI;

public partial class ChatSection : UserControl
{
    private IAuraCoreLLM? _llm;
    private IAIAnalyzerEngine? _aiEngine;
    private bool _initialized;
    private bool _isSending;

    public ChatSection()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        try { _llm = App.Services.GetService<IAuraCoreLLM>(); } catch { }
        try { _aiEngine = App.Services.GetService<IAIAnalyzerEngine>(); } catch { }

        SendButton.Click += OnSendClick;
        InputBox.KeyDown += OnInputKeyDown;

        ApplyLocalization();
        RestoreOrWelcome();

        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        InputBox.Watermark = LocalizationService._("aiChat.inputHint");
        WarningText.Text = LocalizationService._("aiChat.experimentalWarning");
    }

    private void RestoreOrWelcome()
    {
        // Restore chat history from in-memory store
        var history = ChatHistoryStore.Messages;
        if (history.Count > 0)
        {
            foreach (var msg in history)
                AddMessage(msg.Text, isUser: msg.Role == "user");
        }
        else if (_llm is null || !_llm.IsAvailable)
        {
            var text = LocalizationService._("aiChat.modelNotFound");
            AddMessage(text, isUser: false);
            ChatHistoryStore.Add("assistant", text);
        }
        else
        {
            var welcome = LocalizationService._("aiChat.welcome");
            AddMessage(welcome, isUser: false);
            ChatHistoryStore.Add("assistant", welcome);
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isSending)
        {
            e.Handled = true;
            _ = SendMessageAsync();
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (!_isSending)
            _ = SendMessageAsync();
    }

    private async Task SendMessageAsync()
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        InputBox.Text = "";
        AddMessage(text, isUser: true);
        ChatHistoryStore.Add("user", text);

        _isSending = true;
        SendButton.IsEnabled = false;

        try
        {
            var context = BuildContext();
            string response;

            if (_llm is null || !_llm.IsAvailable)
            {
                response = LocalizationService._("aiChat.modelNotFound");
            }
            else
            {
                response = await _llm.AskAsync(text, context);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddMessage(response, isUser: false);
                ChatHistoryStore.Add("assistant", response);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddMessage($"Error: {ex.Message}", isUser: false);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isSending = false;
                SendButton.IsEnabled = true;
            });
        }
    }

    private LlmContext? BuildContext()
    {
        var result = _aiEngine?.LatestResult;
        if (result is null) return null;

        var alerts = new List<string>();
        if (result.CpuAnomaly) alerts.Add("CPU anomaly detected");
        if (result.RamAnomaly) alerts.Add("RAM anomaly detected");
        foreach (var leak in result.MemoryLeaks)
            alerts.Add($"Memory leak: {leak.ProcessName}");

        return new LlmContext(
            CpuPercent: result.CpuAnomalyScore * 100,
            RamPercent: result.RamAnomalyScore * 100,
            DiskPercent: result.DiskPrediction is { } dp ? dp.Confidence * 100 : 0,
            ProfileType: null,
            Language: LocalizationService.CurrentLanguage,
            ActiveAlerts: alerts.Count > 0 ? alerts : null);
    }

    private void AddMessage(string text, bool isUser)
    {
        var bubble = new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(12),
            Padding = new global::Avalonia.Thickness(12, 8),
            MaxWidth = 520,
            Margin = new global::Avalonia.Thickness(0, 2),
            HorizontalAlignment = isUser
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left,
            Background = isUser
                ? new SolidColorBrush(Color.Parse("#8B5CF6")) { Opacity = 0.15 }
                : new SolidColorBrush(Color.Parse("#FFFFFF")) { Opacity = 0.05 },
            BorderThickness = new global::Avalonia.Thickness(1),
            BorderBrush = isUser
                ? new SolidColorBrush(Color.Parse("#8B5CF6")) { Opacity = 0.25 }
                : new SolidColorBrush(Color.Parse("#FFFFFF")) { Opacity = 0.08 }
        };

        bubble.Child = new TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Foreground = isUser
                ? new SolidColorBrush(Color.Parse("#C4B5FD"))
                : new SolidColorBrush(Color.Parse("#D1D5DB"))
        };

        MessagesPanel.Children.Add(bubble);

        // Auto-scroll to bottom
        Dispatcher.UIThread.Post(() =>
        {
            ChatScroll.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
