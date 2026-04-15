using AuraCore.UI.Avalonia.Services.AI;

namespace AuraCore.UI.Avalonia.ViewModels;

public sealed class ModelListItemVM
{
    public ModelListItemVM(ModelDescriptor model, bool isInstalled, bool isSelectable, string? disabledReason)
    {
        Model = model;
        IsInstalled = isInstalled;
        IsSelectable = isSelectable;
        DisabledReason = disabledReason;
    }

    public ModelDescriptor Model { get; }
    public bool IsInstalled { get; }
    public bool IsSelectable { get; }
    public string? DisabledReason { get; }

    public string SizeDisplay => FormatGb(Model.SizeBytes);
    public string RamDisplay => "~" + FormatGb(Model.EstimatedRamBytes);
    public string SpeedDisplay => Model.Speed.ToString().ToUpperInvariant();
    public string TierDisplay => Model.Tier.ToString();
    public bool IsRecommended => Model.IsRecommended;

    private static string FormatGb(long bytes)
    {
        const double GB = 1024d * 1024 * 1024;
        var gb = bytes / GB;
        return gb >= 10 ? $"{gb:F0} GB" : $"{gb:F1} GB";
    }
}
