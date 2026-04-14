using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public sealed class InsightRow
{
    public Geometry? Icon { get; set; }
    public IBrush IconBrush { get; set; } = Brushes.Gray;
    public string Title { get; set; } = string.Empty;
    public IBrush TitleBrush { get; set; } = Brushes.White;
    public string Description { get; set; } = string.Empty;
}
