using System;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using AuraCore.UI.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages.AI;

public partial class ChatSection : UserControl
{
    private IAuraCoreLLM? _llm;
    private IAIAnalyzerEngine? _aiEngine;
    private bool _initialized;
    private bool _isSending;
    private CancellationTokenSource? _reloadCts;

    // ── ReloadAsync helper ─────────────────────────────────────────────────

    public enum ReloadStatus { Ok, Cancelled, Busy, Failed }
    public readonly record struct ReloadResult(ReloadStatus Status, string? Error = null);

    public static async Task<ReloadResult> ApplyModelChangeAsync(
        IAuraCoreLLM llm, LlmConfiguration newConfig, CancellationToken ct)
    {
        try
        {
            await llm.ReloadAsync(newConfig, ct);
            return new ReloadResult(ReloadStatus.Ok);
        }
        catch (OperationCanceledException)
        {
            return new ReloadResult(ReloadStatus.Cancelled);
        }
        catch (InvalidOperationException ex)
        {
            return new ReloadResult(ReloadStatus.Busy, ex.Message);
        }
        catch (Exception ex)
        {
            return new ReloadResult(ReloadStatus.Failed, ex.Message);
        }
    }

    public ChatSection()
    {
        InitializeComponent();
        // Set initial chip placeholder here (in XAML-default is empty to avoid hardcoded scanner offender)
        var chip = this.FindControl<SplitButton>("ModelChip");
        if (chip is not null)
            chip.Content = LocalizationService._("chat.noModelSelected");
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
        BuildModelMenu();

        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
    }

    // ───────── Phase 3 Task 31: Model chip dropdown ─────────

    /// <summary>
    /// Populates the ModelMenuFlyout with installed models (marking the active
    /// one with "●") + a "Download more..." entry that opens ModelManagerDialog
    /// in Manage mode. Also updates the chip label to reflect the current model.
    /// Safe to call multiple times — clears existing items first.
    /// </summary>
    private void BuildModelMenu()
    {
        // MenuFlyout is FlyoutBase (not Control) so it can't be FindControl-ed directly.
        // Access it via SplitButton.Flyout instead.
        var chip = this.FindControl<SplitButton>("ModelChip");
        if (chip is null) return;
        if (chip.Flyout is not MenuFlyout flyout) return;

        flyout.Items.Clear();

        IModelCatalog? catalog = null;
        IInstalledModelStore? installed = null;
        AppSettings? settings = null;
        try
        {
            catalog = App.Services.GetService<IModelCatalog>();
            installed = App.Services.GetService<IInstalledModelStore>();
            settings = App.Services.GetService<AppSettings>();
        }
        catch { /* design-time / DI not ready — menu stays empty, chip shows placeholder */ }

        if (catalog is null || installed is null || settings is null)
        {
            chip.Content = LocalizationService._("chat.noModelSelected");
            // Add a disabled placeholder item so clicking the dropdown still gives feedback
            flyout.Items.Add(new MenuItem
            {
                Header = LocalizationService._("chat.noModelsInstalled"),
                IsEnabled = false,
            });
            flyout.Items.Add(new Separator());
            var downloadMore0 = new MenuItem { Header = "\u2B07 Download models..." };
            downloadMore0.Click += (_, _) => OnOpenModelManager();
            flyout.Items.Add(downloadMore0);
            return;
        }

        var installedModels = installed.Enumerate();
        if (installedModels.Count == 0)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = LocalizationService._("chat.noModelsInstalled"),
                IsEnabled = false,
            });
        }
        else
        {
            foreach (var im in installedModels)
            {
                var descriptor = catalog.FindById(im.ModelId);
                if (descriptor is null) continue;

                var isActive = settings.ActiveChatModelId == descriptor.Id;
                var sizeGb = im.SizeBytes / (1024d * 1024 * 1024);
                var item = new MenuItem
                {
                    Header = $"{(isActive ? "\u25CF " : "  ")}{descriptor.DisplayName}  ({sizeGb:F1} GB)",
                    Tag = descriptor.Id,
                };
                item.Click += (_, _) => OnSwitchModel(descriptor.Id);
                flyout.Items.Add(item);
            }
        }

        flyout.Items.Add(new Separator());
        var downloadMore = new MenuItem { Header = "\u2B07 Download more models..." };
        downloadMore.Click += (_, _) => OnOpenModelManager();
        flyout.Items.Add(downloadMore);

        // Update chip content to reflect the active installed model
        var active = installedModels.FirstOrDefault(im => im.ModelId == settings.ActiveChatModelId);
        var activeDescriptor = active is not null ? catalog.FindById(active.ModelId) : null;
        chip.Content = activeDescriptor is not null
            ? $"\u2699 {activeDescriptor.DisplayName} \u25BE"
            : "\u2699 No model selected \u25BE";
    }

    /// <summary>
    /// Switches the active chat model in AppSettings, saves to disk, rebuilds
    /// the menu so the active indicator moves, then hot-reloads the inference
    /// engine via <see cref="ApplyModelChangeAsync"/> — no app restart required.
    /// </summary>
    private async void OnSwitchModel(string modelId)
    {
        AppSettings? settings = null;
        IInstalledModelStore? installed = null;
        try
        {
            settings  = App.Services.GetService<AppSettings>();
            installed = App.Services.GetService<IInstalledModelStore>();
        }
        catch { }
        if (settings is null) return;

        if (settings.ActiveChatModelId == modelId) return; // no-op when picking the same model

        settings.ActiveChatModelId = modelId;
        try { settings.Save(); } catch { /* persistence best-effort; menu still updates in-memory */ }

        BuildModelMenu();

        if (_llm is null)
        {
            var failMsg = LocalizationService.Get("chat.reload.failed");
            AddMessage(failMsg, isUser: false);
            ChatHistoryStore.Add("assistant", failMsg);
            return;
        }

        // Resolve the model file path from the installed store, falling back to
        // the model-id filename so the engine can surface a "file not found" error.
        var modelFile = installed?.GetFile(modelId);
        var modelPath = modelFile?.FullName ?? (modelId + ".gguf");
        var newConfig = new LlmConfiguration(modelPath);

        // Cancel any in-flight reload before starting the new one.
        _reloadCts?.Cancel();
        _reloadCts?.Dispose();
        _reloadCts = new CancellationTokenSource();

        var result = await ApplyModelChangeAsync(_llm, newConfig, _reloadCts.Token);
        var toastKey = result.Status switch
        {
            ReloadStatus.Ok        => "chat.reload.success",
            ReloadStatus.Cancelled => "chat.reload.cancelled",
            ReloadStatus.Busy      => "chat.reload.busy",
            ReloadStatus.Failed    => "chat.reload.failed",
            _ => "chat.reload.failed"
        };
        var toast = LocalizationService.Get(toastKey);
        AddMessage(toast, isUser: false);
        ChatHistoryStore.Add("assistant", toast);
    }

    /// <summary>
    /// Opens ModelManagerDialog in Manage mode inside a centered modal window.
    /// After the dialog closes (user may have downloaded another model), the
    /// menu is rebuilt to reflect the new catalog of installed models.
    /// </summary>
    private async void OnOpenModelManager()
    {
        var ownerWindow = this.FindAncestorOfType<Window>();
        if (ownerWindow is null) return;

        IModelCatalog? catalog = null;
        IInstalledModelStore? installed = null;
        IModelDownloadService? downloader = null;
        try
        {
            catalog = App.Services.GetService<IModelCatalog>();
            installed = App.Services.GetService<IInstalledModelStore>();
            downloader = App.Services.GetService<IModelDownloadService>();
        }
        catch { }

        if (catalog is null || installed is null || downloader is null) return;

        var vm = new ModelManagerDialogViewModel(
            catalog, installed,
            ModelManagerDialogMode.Manage,
            downloader);

        var managerDialog = new Window
        {
            Title = "Manage AI Models",
            Width = 580, Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ModelManagerDialog(vm),
        };

        vm.RequestClose = _ => managerDialog.Close();
        await managerDialog.ShowDialog(ownerWindow);

        // Refresh after dialog closes — user may have downloaded a new model
        BuildModelMenu();
    }

    private void ApplyLocalization()
    {
        if (ChatHeaderTitle is not null)
            ChatHeaderTitle.Text = LocalizationService.Get("chat.headerTitle");
        if (TopWarningText is not null)
            TopWarningText.Text = LocalizationService.Get("aiFeatures.chat.warningBanner");
        if (InputBox is not null)
            InputBox.Watermark = LocalizationService.Get("aiChat.inputHint");
        if (WarningText is not null)
            WarningText.Text = LocalizationService.Get("aiChat.experimentalWarning");
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
