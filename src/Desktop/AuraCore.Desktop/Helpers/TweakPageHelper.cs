using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Helpers;

internal static class TweakPageHelper
{
    internal static Border CreateTweakCard(
        string name, string description, string risk, bool isApplied,
        string tweakId, Dictionary<string, bool> selections, Action onChanged)
    {
        selections[tweakId] = false;

        var card = new Border
        {
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 10, 16, 10),
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(0.5)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var toggle = new ToggleSwitch
        {
            IsOn = isApplied,
            OnContent = "", OffContent = "",
            VerticalAlignment = VerticalAlignment.Center
        };
        var wasApplied = isApplied;
        toggle.Toggled += (s, e) =>
        {
            selections[tweakId] = toggle.IsOn != wasApplied;
            onChanged();
        };
        Grid.SetColumn(toggle, 0);
        grid.Children.Add(toggle);

        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13
        });
        info.Children.Add(new TextBlock
        {
            Text = description, FontSize = 11, Opacity = 0.6, TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var riskColor = risk switch
        {
            "Safe" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
            "Caution" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
            _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
        };
        var badge = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = risk.ToUpper(), FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(riskColor)
        };
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        if (isApplied)
        {
            var statusBadge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 33, 150, 243)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadge.Child = new TextBlock { Text = "ACTIVE", FontSize = 9, Opacity = 0.7 };
            Grid.SetColumn(statusBadge, 3);
            grid.Children.Add(statusBadge);
        }

        card.Child = grid;
        return card;
    }
}
