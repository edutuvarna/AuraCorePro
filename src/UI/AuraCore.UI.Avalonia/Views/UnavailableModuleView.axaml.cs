using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.UI.Avalonia.Views;

/// <summary>
/// Phase 6.16: full-page diagnostic view for unavailable modules.
/// Shows module name, category-specific title, reason, copyable remediation
/// command, and a "Try Again" button that re-runs the navigator's resolve.
/// </summary>
public partial class UnavailableModuleView : UserControl
{
    private string _moduleName = string.Empty;
    private ModuleAvailability _availability = ModuleAvailability.Available;
    private Func<Task>? _onTryAgain;

    public UnavailableModuleView()
    {
        InitializeComponent();
        // Subscribe to language changes so re-rendered Render() picks up TR/EN switch.
        LocalizationService.LanguageChanged += Render;
        DetachedFromVisualTree += (_, _) => LocalizationService.LanguageChanged -= Render;
    }

    public UnavailableModuleView(string moduleName, ModuleAvailability availability, Func<Task>? onTryAgain = null) : this()
    {
        SetState(moduleName, availability, onTryAgain);
    }

    public void SetState(string moduleName, ModuleAvailability availability, Func<Task>? onTryAgain = null)
    {
        _moduleName = moduleName;
        _availability = availability;
        _onTryAgain = onTryAgain;
        Render();
    }

    private void Render()
    {
        var moduleNameText    = this.FindControl<TextBlock>("ModuleNameText");
        var categoryTitleText = this.FindControl<TextBlock>("CategoryTitleText");
        var reasonText        = this.FindControl<TextBlock>("ReasonText");
        var remediationPanel  = this.FindControl<Border>("RemediationPanel");
        var remediationLabel  = this.FindControl<TextBlock>("RemediationLabel");
        var remediationText   = this.FindControl<SelectableTextBlock>("RemediationText");
        var copyButton        = this.FindControl<Button>("CopyButton");
        var tryAgainButton    = this.FindControl<Button>("TryAgainButton");

        if (moduleNameText is not null) moduleNameText.Text = _moduleName;
        if (categoryTitleText is not null)
            categoryTitleText.Text = LocalizationService.Get($"unavailable.title.{_availability.Category}");
        if (reasonText is not null) reasonText.Text = _availability.Reason ?? string.Empty;

        if (string.IsNullOrEmpty(_availability.RemediationCommand))
        {
            if (remediationPanel is not null) remediationPanel.IsVisible = false;
        }
        else
        {
            if (remediationPanel is not null) remediationPanel.IsVisible = true;
            if (remediationLabel is not null) remediationLabel.Text = LocalizationService.Get("unavailable.remediation");
            if (remediationText is not null) remediationText.Text = _availability.RemediationCommand;
            if (copyButton is not null) copyButton.Content = LocalizationService.Get("unavailable.copy");
        }

        if (tryAgainButton is not null)
        {
            tryAgainButton.Content = LocalizationService.Get("unavailable.tryAgain");
            tryAgainButton.IsVisible = _onTryAgain is not null;
        }
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_availability.RemediationCommand)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(_availability.RemediationCommand);
        var copyButton = this.FindControl<Button>("CopyButton");
        if (copyButton is not null) copyButton.Content = LocalizationService.Get("unavailable.copied");
        await Task.Delay(1500);
        if (copyButton is not null) copyButton.Content = LocalizationService.Get("unavailable.copy");
    }

    private async void OnTryAgainClick(object? sender, RoutedEventArgs e)
    {
        if (_onTryAgain is null) return;
        try { await _onTryAgain(); }
        catch { /* defensive — Try-Again must not crash the shell */ }
    }
}
