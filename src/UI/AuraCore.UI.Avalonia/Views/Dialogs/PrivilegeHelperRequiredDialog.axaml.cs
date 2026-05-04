using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

/// <summary>
/// Phase 6.17: modal dialog shown when a privileged action is attempted but
/// the privilege helper isn't running. Mirrors UnavailableModuleView UX with
/// title / reason / copyable remediation / documentation link / Try Again /
/// Close. The "Try Again" button just closes the dialog with DialogResult
/// = true; the caller (PrivilegedActionGuard) returns false in either case
/// so the calling module's pre-flight short-circuits to OperationResult.Skipped.
/// (Auto-refresh on helper install is Phase 6.18+.)
/// </summary>
public partial class PrivilegeHelperRequiredDialog : Window
{
    private string _actionDescription = string.Empty;
    private string _remediationCommand = string.Empty;

    public PrivilegeHelperRequiredDialog()
    {
        InitializeComponent();
        LocalizationService.LanguageChanged += Render;
        Closed += (_, _) => LocalizationService.LanguageChanged -= Render;
    }

    public PrivilegeHelperRequiredDialog(string actionDescription, string remediationCommand) : this()
    {
        _actionDescription = actionDescription;
        _remediationCommand = remediationCommand;
        Render();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Render()
    {
        var title = this.FindControl<TextBlock>("TitleText");
        var actionDesc = this.FindControl<TextBlock>("ActionDescText");
        var reason = this.FindControl<TextBlock>("ReasonText");
        var remediation = this.FindControl<SelectableTextBlock>("RemediationText");
        var copyBtn = this.FindControl<Button>("CopyButton");
        var docsBtn = this.FindControl<Button>("DocsButton");
        var tryAgainBtn = this.FindControl<Button>("TryAgainButton");
        var closeBtn = this.FindControl<Button>("CloseButton");

        Title = LocalizationService.Get("privhelper.dialog.title");
        if (title is not null) title.Text = LocalizationService.Get("privhelper.dialog.title");
        if (actionDesc is not null) actionDesc.Text = _actionDescription;
        if (reason is not null) reason.Text = LocalizationService.Get("privhelper.dialog.reason");
        if (remediation is not null) remediation.Text = _remediationCommand;
        if (copyBtn is not null) copyBtn.Content = LocalizationService.Get("unavailable.copy");
        if (docsBtn is not null) docsBtn.Content = LocalizationService.Get("privhelper.dialog.docs");
        if (tryAgainBtn is not null) tryAgainBtn.Content = LocalizationService.Get("privhelper.dialog.tryAgain");
        if (closeBtn is not null) closeBtn.Content = LocalizationService.Get("privhelper.dialog.closeBtn");
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrEmpty(_remediationCommand)) return;
        await clipboard.SetTextAsync(_remediationCommand);
        var copyBtn = this.FindControl<Button>("CopyButton");
        if (copyBtn is not null) copyBtn.Content = LocalizationService.Get("unavailable.copied");
        await Task.Delay(1500);
        if (copyBtn is not null) copyBtn.Content = LocalizationService.Get("unavailable.copy");
    }

    private void OnDocsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Phase 6.18 will publish the real docs URL; placeholder for now.
            Process.Start(new ProcessStartInfo("https://docs.auracore.pro/linux/privilege-helper") { UseShellExecute = true })?.Dispose();
        }
        catch { /* best-effort */ }
    }

    private void OnTryAgainClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(false);
}
