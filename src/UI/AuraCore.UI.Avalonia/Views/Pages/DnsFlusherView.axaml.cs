using AuraCore.Module.DnsFlusher;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DnsFlusherView : UserControl
{
    public DnsFlusherView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try
                {
                    var engine = App.Services.GetRequiredService<DnsFlusherModule>();
                    DataContext = new DnsFlusherViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
            // Auto-scan on load (user-directed hero UX: no manual Scan button).
            (DataContext as DnsFlusherViewModel)?.TriggerInitialScan();
        };
    }

    private void ApplyLocalizedTexts()
    {
        string L(string k) => LocalizationService._(k);
        if (this.FindControl<Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("nav.dnsFlusher");
            h.Subtitle = L("dnsFlusher.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt) pt.Text = L("nav.dnsFlusher");
        if (this.FindControl<TextBlock>("HeroDescription") is { } hd) hd.Text = L("dnsFlusher.hero.description");
        if (this.FindControl<Button>("FlushBtn") is { } fb) fb.Content = L("dnsFlusher.hero.action");
        if (this.FindControl<TextBlock>("WhenToUseHeading") is { } wh) wh.Text = L("dnsFlusher.whenToUse.heading");
        if (this.FindControl<TextBlock>("WhenToUseItem1") is { } w1) w1.Text = L("dnsFlusher.whenToUse.item1");
        if (this.FindControl<TextBlock>("WhenToUseItem2") is { } w2) w2.Text = L("dnsFlusher.whenToUse.item2");
        if (this.FindControl<TextBlock>("WhenToUseItem3") is { } w3) w3.Text = L("dnsFlusher.whenToUse.item3");
        if (this.FindControl<TextBlock>("WhenToUseItem4") is { } w4) w4.Text = L("dnsFlusher.whenToUse.item4");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw) pw.Text = L("dnsFlusher.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
