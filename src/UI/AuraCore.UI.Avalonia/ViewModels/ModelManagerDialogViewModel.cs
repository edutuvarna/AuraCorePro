using AuraCore.UI.Avalonia.Services.AI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

public enum ModelManagerDialogMode { OptIn, Manage }

public sealed class ModelManagerDialogViewModel : INotifyPropertyChanged
{
    private readonly IModelCatalog _catalog;
    private readonly IInstalledModelStore _installed;
    private readonly IModelDownloadService? _downloader;
    private readonly long _physicalRamBytes;

    public ModelManagerDialogViewModel(
        IModelCatalog catalog,
        IInstalledModelStore installed,
        ModelManagerDialogMode mode,
        IModelDownloadService? downloader = null,
        long? physicalRamBytes = null)
    {
        _catalog = catalog;
        _installed = installed;
        _downloader = downloader;
        _physicalRamBytes = physicalRamBytes ?? DetectPhysicalRam();
        Mode = mode;

        Models = BuildItems();

        DownloadCommand = new DelegateCommand<object?>(async _ => await DownloadAsync(), _ => CanDownload);
        CancelDownloadCommand = new DelegateCommand<object?>(_ => _downloadCts?.Cancel());
        CancelDialogCommand = new DelegateCommand<object?>(_ => RequestClose?.Invoke(null));
    }

    public ModelManagerDialogMode Mode { get; }
    public IReadOnlyList<ModelListItemVM> Models { get; }

    public string Title => Mode == ModelManagerDialogMode.OptIn
        ? "Choose your AI model"
        : "Manage AI Models";

    public string Subtitle => Mode == ModelManagerDialogMode.OptIn
        ? "Please select a model to download. Models are pulled from AuraCore cloud."
        : "Download additional models or switch active model.";

    private ModelListItemVM? _selectedModel;
    public ModelListItemVM? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (ReferenceEquals(_selectedModel, value)) return;
            _selectedModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanDownload));
            (DownloadCommand as DelegateCommand<object?>)?.RaiseCanExecuteChanged();
        }
    }

    public bool CanDownload =>
        _selectedModel is not null &&
        _selectedModel.IsSelectable &&
        !_selectedModel.IsInstalled &&
        _downloader is not null &&
        _activeDownload is null;

    private DownloadProgress? _activeDownload;
    public DownloadProgress? ActiveDownload
    {
        get => _activeDownload;
        private set
        {
            _activeDownload = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    public bool IsDownloading => _activeDownload is not null;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { _errorMessage = value; OnPropertyChanged(); }
    }

    public ICommand DownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand CancelDialogCommand { get; }

    public Action<ModelDescriptor?>? RequestClose { get; set; }

    private CancellationTokenSource? _downloadCts;

    private async Task DownloadAsync()
    {
        if (_selectedModel is null || _downloader is null) return;

        ErrorMessage = null;
        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<DownloadProgress>(p => ActiveDownload = p);

        try
        {
            await _downloader.DownloadAsync(_selectedModel.Model, progress, _downloadCts.Token);
            ActiveDownload = null;
            RequestClose?.Invoke(_selectedModel.Model);
        }
        catch (OperationCanceledException)
        {
            ActiveDownload = null;
        }
        catch (ModelSizeMismatchException)
        {
            ActiveDownload = null;
            ErrorMessage = "Downloaded file is corrupted (size mismatch). Please try again.";
        }
        catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("403"))
        {
            ActiveDownload = null;
            ErrorMessage = "Download blocked by server. Please contact support.";
        }
        catch (Exception ex)
        {
            ActiveDownload = null;
            ErrorMessage = $"Couldn't reach models.auracore.pro. Check your connection. ({ex.GetType().Name})";
        }
    }

    private IReadOnlyList<ModelListItemVM> BuildItems()
    {
        return _catalog.All.Select(m =>
        {
            var isInstalled = _installed.IsInstalled(m.Id);
            var hasEnoughRam = _physicalRamBytes >= m.EstimatedRamBytes;
            var selectable = hasEnoughRam;
            string? reason = null;
            if (!hasEnoughRam)
            {
                var needGb = Math.Round((double)m.EstimatedRamBytes / (1024 * 1024 * 1024), 0);
                reason = $"Needs {needGb} GB RAM";
            }
            return new ModelListItemVM(m, isInstalled, selectable, reason);
        }).ToList();
    }

    private static long DetectPhysicalRam()
    {
        try
        {
            var info = global::System.GC.GetGCMemoryInfo();
            return info.TotalAvailableMemoryBytes;
        }
        catch { return 16L * 1024 * 1024 * 1024; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
