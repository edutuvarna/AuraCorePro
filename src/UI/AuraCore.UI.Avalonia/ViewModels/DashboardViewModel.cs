using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Controls;

namespace AuraCore.UI.Avalonia.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private double _cpu, _ram, _disk, _gpu;
    private double _healthScore = 100.0;
    private string _healthLabel = "Excellent";
    private GpuInfo? _gpuInfo;
    private string _osName = "";
    private string _cpuName = "";
    private double _ramTotalGb;
    private int _cortexDaysActive = 0;
    private bool _cortexOn = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<InsightRow> Insights { get; } = new()
    {
        new InsightRow
        {
            Title = "Cortex is Learning",
            Description = "Insights will appear after 60 seconds of monitoring.",
            TitleBrush = global::Avalonia.Media.Brushes.Violet,
        }
    };

    public double CpuPercent       { get => _cpu;   set => Set(ref _cpu, value); }
    public double RamPercent       { get => _ram;   set => Set(ref _ram, value); }
    public double DiskPercent      { get => _disk;  set => Set(ref _disk, value); }
    public double GpuPercent       { get => _gpu;   set => Set(ref _gpu, value); }
    public double HealthScore      { get => _healthScore; set => Set(ref _healthScore, value); }
    public string HealthLabel      { get => _healthLabel; set => Set(ref _healthLabel, value); }

    public GpuInfo? GpuInfo        { get => _gpuInfo; private set { _gpuInfo = value; OnChanged(nameof(GpuInfo)); OnChanged(nameof(GpuVisible)); OnChanged(nameof(GpuName)); } }
    public bool    GpuVisible      => _gpuInfo is not null;
    public string  GpuName         => _gpuInfo?.Name ?? "";

    public string OsName           { get => _osName;      set => Set(ref _osName, value); }
    public string CpuName          { get => _cpuName;     set => Set(ref _cpuName, value); }
    public double RamTotalGb       { get => _ramTotalGb;  set => Set(ref _ramTotalGb, value); }

    public int CortexDaysActive    { get => _cortexDaysActive; set => Set(ref _cortexDaysActive, value); }
    public bool CortexOn           { get => _cortexOn;         set => Set(ref _cortexOn, value); }

    public string SystemSummary =>
        $"{OsName} · {CpuName} · {RamTotalGb:0.#} GB";

    public string CortexStatusText =>
        $"Cortex · Learning your patterns (day {CortexDaysActive})";

    public void SetGpuInfo(GpuInfo? info) => GpuInfo = info;

    public void UpdateInsights(IEnumerable<InsightRow> fresh)
    {
        Insights.Clear();
        foreach (var r in fresh) Insights.Add(r);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnChanged(prop);
            if (prop is nameof(OsName) or nameof(CpuName) or nameof(RamTotalGb))
                OnChanged(nameof(SystemSummary));
            if (prop is nameof(CortexDaysActive))
                OnChanged(nameof(CortexStatusText));
        }
    }

    private void OnChanged(string? p) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
