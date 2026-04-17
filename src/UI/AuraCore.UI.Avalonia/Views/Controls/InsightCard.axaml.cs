using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class InsightCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InsightCard, string>(nameof(Title), "Insights");

    public static readonly StyledProperty<string> UpdatedAtProperty =
        AvaloniaProperty.Register<InsightCard, string>(nameof(UpdatedAt), string.Empty);

    public static readonly StyledProperty<ObservableCollection<InsightRow>> RowsProperty =
        AvaloniaProperty.Register<InsightCard, ObservableCollection<InsightRow>>(nameof(Rows));

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string UpdatedAt { get => GetValue(UpdatedAtProperty); set => SetValue(UpdatedAtProperty, value); }
    public ObservableCollection<InsightRow> Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    public InsightCard()
    {
        InitializeComponent();
        // Per-instance collection — prevents shared default across instances.
        SetCurrentValue(RowsProperty, new ObservableCollection<InsightRow>());
    }
}
