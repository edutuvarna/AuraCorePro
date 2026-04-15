using System;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class SmartOptimizePlaceholderDialog : Window
{
    /// <summary>Fired when user clicks "Go to AI Recommendations". Host should navigate there.</summary>
    public event EventHandler? GoToRecommendationsRequested;

    public SmartOptimizePlaceholderDialog()
    {
        InitializeComponent();
    }

    private void GoToRecommendations_Click(object? sender, RoutedEventArgs e)
    {
        GoToRecommendationsRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void Dismiss_Click(object? sender, RoutedEventArgs e) => Close();
}
