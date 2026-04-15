using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.ViewModels;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AIFeatureCard : UserControl
{
    public AIFeatureCard()
    {
        InitializeComponent();
        PointerPressed += OnCardClick;
    }

    private void OnCardClick(object? sender, PointerPressedEventArgs e)
    {
        // Toggle handles its own pointer events; ignore when origin is the toggle.
        if (e.Source is Control src && FindAncestor<AuraToggle>(src) is not null)
            return;

        if (DataContext is AIFeatureCardVM vm && vm.NavigateToDetail?.CanExecute(vm.Key) == true)
            vm.NavigateToDetail.Execute(vm.Key);
    }

    private static T? FindAncestor<T>(Control start) where T : Control
    {
        Control? current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = current.Parent as Control;
        }
        return null;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
