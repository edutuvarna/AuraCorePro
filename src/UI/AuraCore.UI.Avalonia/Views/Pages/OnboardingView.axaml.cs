using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class OnboardingView : UserControl
{
    private int _currentStep;

    private static readonly string CompletedPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "onboarding_done.flag");

    public static bool IsCompleted => File.Exists(CompletedPath);

    private sealed record Step(string Icon, string Title, string Description, string Detail);

    private readonly List<Step> _steps = new()
    {
        new("\u2665",
            LocalizationService._("onb.welcomeTitle"),
            LocalizationService._("onb.welcomeDesc"),
            LocalizationService._("onb.welcomeDetail")),

        new("\u2302",
            LocalizationService._("onb.dashboardTitle"),
            LocalizationService._("onb.dashboardDesc"),
            LocalizationService._("onb.dashboardDetail")),

        new("\u2702",
            LocalizationService._("onb.cleanTitle"),
            LocalizationService._("onb.cleanDesc"),
            LocalizationService._("onb.cleanDetail")),

        new("\u2B50",
            LocalizationService._("onb.gamingTitle"),
            LocalizationService._("onb.gamingDesc"),
            LocalizationService._("onb.gamingDetail")),

        new("\u2699",
            LocalizationService._("onb.customizeTitle"),
            LocalizationService._("onb.customizeDesc"),
            LocalizationService._("onb.customizeDetail")),

        new("\u2605",
            LocalizationService._("onb.smartTitle"),
            LocalizationService._("onb.smartDesc"),
            LocalizationService._("onb.smartDetail")),
    };

    public OnboardingView()
    {
        InitializeComponent();
        BuildDots();
        ShowStep(0);
    }

    private void BuildDots()
    {
        StepDots.Children.Clear();
        for (int i = 0; i < _steps.Count; i++)
        {
            var dot = new Border
            {
                Width = 10, Height = 10,
                CornerRadius = new global::Avalonia.CornerRadius(5),
                Background = new SolidColorBrush(Color.Parse("#3D808080")),
                Tag = i
            };
            StepDots.Children.Add(dot);
        }
    }

    private void ShowStep(int index)
    {
        _currentStep = Math.Clamp(index, 0, _steps.Count - 1);
        var step = _steps[_currentStep];

        StepIcon.Text = step.Icon;
        StepTitle.Text = step.Title;
        StepDescription.Text = step.Description;
        StepDetail.Text = step.Detail;
        StepCounter.Text = $"{_currentStep + 1} / {_steps.Count}";

        BackBtn.IsVisible = _currentStep > 0;

        bool isLast = _currentStep == _steps.Count - 1;
        NextBtn.Content = isLast ? LocalizationService._("onb.getStarted") : LocalizationService._("onb.next");
        BackBtn.Content = LocalizationService._("onb.back");
        SkipBtn.Content = LocalizationService._("onb.skip");

        // Update dots
        for (int i = 0; i < StepDots.Children.Count; i++)
        {
            if (StepDots.Children[i] is Border dot)
            {
                bool isActive = i == _currentStep;
                dot.Width = isActive ? 24 : 10;
                dot.Background = new SolidColorBrush(
                    isActive ? Color.Parse("#00D4AA") : Color.Parse("#3D808080"));
            }
        }
    }

    private void NextBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentStep < _steps.Count - 1)
            ShowStep(_currentStep + 1);
        else
            CompleteOnboarding();
    }

    private void BackBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 0) ShowStep(_currentStep - 1);
    }

    private void SkipBtn_Click(object? sender, RoutedEventArgs e)
    {
        CompleteOnboarding();
    }

    private void CompleteOnboarding()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CompletedPath)!);
            File.WriteAllText(CompletedPath, DateTime.Now.ToString("o"));
        }
        catch { /* non-critical */ }

        // Signal parent (MainWindow) to switch to Dashboard
        OnboardingCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when onboarding finishes (skip or complete)</summary>
    public event EventHandler? OnboardingCompleted;
}
