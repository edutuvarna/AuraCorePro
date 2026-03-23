using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Engines;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Application.Interfaces.Simulation;

namespace AuraCore.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IOptimizationEngine _engine;
    private readonly ISystemMonitorEngine _monitor;
    private readonly IAIRecommenderEngine _recommender;
    private readonly ISimulationEngine _simulation;
    private readonly IDiagnosticsEngine _diagnostics;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isScanning;

    public DashboardViewModel(
        IOptimizationEngine engine,
        ISystemMonitorEngine monitor,
        IAIRecommenderEngine recommender,
        ISimulationEngine simulation,
        IDiagnosticsEngine diagnostics)
    {
        _engine = engine;
        _monitor = monitor;
        _recommender = recommender;
        _simulation = simulation;
        _diagnostics = diagnostics;
    }

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        IsScanning = true;
        StatusText = "Scanning all modules...";
        try
        {
            var results = await _engine.ScanAllAsync(new ScanOptions());
            var total = results.Sum(r => r.ItemsFound);
            StatusText = $"Scan complete — {total} items found across {results.Count} modules";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}

public static class ViewModelRegistration
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<DashboardViewModel>();
        return services;
    }
}
