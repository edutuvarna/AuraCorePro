using AuraCore.Desktop;
using AuraCore.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class OnboardingPage : Page
{
    private int _currentStep;

    private static readonly string CompletedPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "onboarding_done.flag");

    public static bool IsCompleted => File.Exists(CompletedPath);

    private sealed record Step(string Icon, string Title, string Description, string Detail);

    private readonly List<Step> _steps = new()
    {
        new("\uE95E",
            "Welcome to Aura Core Pro",
            "Your all-in-one Windows optimization toolkit powered by intelligent analysis. 12 specialized modules work together to keep your system fast, clean, and customized.",
            "Let's take a quick tour of what you can do."),

        new("\uE80F",
            "Live Dashboard",
            "Your dashboard shows real-time CPU, RAM, and disk usage with a health score. Quick action buttons let you scan for junk, optimize RAM, or run a health check in one click.",
            "The dashboard refreshes every 3 seconds — always up to date."),

        new("\uEA99",
            "Clean & Optimize",
            "Junk Cleaner removes temporary files and caches. RAM Optimizer reclaims wasted memory. Storage Compression uses NTFS compression to save disk space transparently. Registry Optimizer cleans invalid entries with automatic backups.",
            "Every operation is safe and reversible."),

        new("\uE7FC",
            "Gaming Mode",
            "One click to maximize gaming performance: switches to high-performance power plan, silences notifications, suspends background apps, and boosts process priority. Auto-detect monitors 60+ games and activates automatically.",
            "Create per-game profiles for customized optimization per title."),

        new("\uE771",
            "Customize Your Windows",
            "Restore the classic right-click menu, hide taskbar clutter like Widgets and Copilot, show file extensions in Explorer, and more. App Installer manages 127+ apps through WinGet with export/import support.",
            "All tweaks work on both Windows 10 and Windows 11."),

        new("\uE713",
            "Smart & Automated",
            "AI Recommendations analyzes your system and suggests improvements. Auto-Scheduling runs modules in the background when your PC is idle. The notification center keeps you informed about everything.",
            "Choose your theme (dark/light) and language (EN/TR) in Settings."),
    };

    public OnboardingPage()
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
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 128, 128, 128)),
                Tag = i
            };
            StepDots.Children.Add(dot);
        }
    }

    private void ShowStep(int index)
    {
        _currentStep = Math.Clamp(index, 0, _steps.Count - 1);
        var step = _steps[_currentStep];

        StepIcon.Glyph = step.Icon;
        StepTitle.Text = step.Title;
        StepDescription.Text = step.Description;
        StepDetail.Text = step.Detail;
        StepCounter.Text = $"{_currentStep + 1} / {_steps.Count}";

        BackBtn.Visibility = _currentStep > 0 ? Visibility.Visible : Visibility.Collapsed;

        bool isLast = _currentStep == _steps.Count - 1;
        NextBtn.Content = isLast ? "Get Started" : "Next";

        // Update dots
        for (int i = 0; i < StepDots.Children.Count; i++)
        {
            if (StepDots.Children[i] is Border dot)
            {
                var isActive = i == _currentStep;
                dot.Width = isActive ? 24 : 10;
                dot.Background = new SolidColorBrush(isActive
                    ? Windows.UI.Color.FromArgb(255, 21, 101, 192)
                    : Windows.UI.Color.FromArgb(60, 128, 128, 128));
            }
        }
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < _steps.Count - 1)
        {
            ShowStep(_currentStep + 1);
        }
        else
        {
            CompleteOnboarding();
        }
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 0) ShowStep(_currentStep - 1);
    }

    private void SkipBtn_Click(object sender, RoutedEventArgs e)
    {
        CompleteOnboarding();
    }

    private void CompleteOnboarding()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CompletedPath)!);
            File.WriteAllText(CompletedPath, DateTime.Now.ToString("o"));
        }
        catch { }

        // Navigate to Dashboard and sync NavView selection
        if (this.Frame is Frame frame)
        {
            frame.Navigate(typeof(DashboardPage));

            // Sync MainWindow NavView to Dashboard
            if (App.MainWindow is MainWindow mainWindow)
            {
                var navView = mainWindow.Content is Grid grid
                    ? grid.Children.OfType<NavigationView>().FirstOrDefault()
                    : null;
                if (navView is not null && navView.MenuItems.Count > 0)
                    navView.SelectedItem = navView.MenuItems[0];
            }
        }
    }
}
