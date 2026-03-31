using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class FontManagerView : UserControl
{
    private List<string> _allFonts = new();

    public FontManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => { LoadFonts(); ApplyLocalization(); };
        PreviewText.TextChanged += (s, e) => RenderFonts(_allFonts);
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void LoadFonts()
    {
        _allFonts = FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(f => f).ToList();
        FontCount.Text = $"{_allFonts.Count} fonts";
        RenderFonts(_allFonts);
    }

    private void Search_Changed(object? sender, TextChangedEventArgs e)
    {
        var q = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(q) ? _allFonts
            : _allFonts.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        FontCount.Text = $"{filtered.Count} fonts";
        RenderFonts(filtered);
    }

    private void RenderFonts(List<string> fonts)
    {
        var preview = PreviewText.Text ?? "The quick brown fox jumps over the lazy dog.";
        FontList.ItemsSource = fonts.Take(100).Select(f =>
        {
            return new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(14, 10),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = f, FontSize = 12, FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse("#00D4AA")) },
                        new TextBlock { Text = preview, FontSize = 18,
                            FontFamily = new FontFamily(f),
                            Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
                            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }
                    }
                }
            };
        }).ToList();
    }

    private void ApplyLocalization() { PageTitle.Text = LocalizationService._("nav.fontManager"); }
}
