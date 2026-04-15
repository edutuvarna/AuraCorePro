using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

public sealed class ChatOptInDialogViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;

    public ChatOptInDialogViewModel(AppSettings settings)
    {
        _settings = settings;
        _currentStep = settings.ChatOptInAcknowledged ? 2 : 1;
        ContinueFromStep1 = new DelegateCommand<object?>(_ => OnContinueFromStep1());
        CancelFromStep1   = new DelegateCommand<object?>(_ => Close(accepted: false));
        CancelFromStep2   = new DelegateCommand<object?>(_ => Close(accepted: false));
    }

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
        }
    }

    public bool IsStep1 => _currentStep == 1;
    public bool IsStep2 => _currentStep == 2;

    public ICommand ContinueFromStep1 { get; }
    public ICommand CancelFromStep1 { get; }
    public ICommand CancelFromStep2 { get; }

    public Action<bool>? RequestClose { get; set; }

    public void CompleteFromStep2(string modelId)
    {
        _settings.ActiveChatModelId = modelId;
        _settings.ChatEnabled = true;
        _settings.Save();
        Close(accepted: true);
    }

    private void OnContinueFromStep1()
    {
        _settings.ChatOptInAcknowledged = true;
        _settings.Save();
        CurrentStep = 2;
    }

    private void Close(bool accepted) => RequestClose?.Invoke(accepted);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
