using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// Non-generic synchronous <see cref="ICommand"/> helper.
/// Shared by all view-models in this assembly — originally lived inside
/// JournalCleanerViewModel (Phase 4.3.1); lifted to its own file in Phase 4.3.2
/// when SnapFlatpakCleanerViewModel needed the same primitive.
/// </summary>
public sealed class DelegateCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Async <see cref="ICommand"/> that fires-and-forgets a Task.
/// <see cref="CanExecute"/> is re-queried on <see cref="RaiseCanExecuteChanged"/>.
/// Exceptions in the inner task are swallowed here; the hosting VM is expected to
/// catch its own exceptions (this catch is only a last-resort safety net so the
/// UI thread never sees an unhandled async-void).
/// </summary>
public sealed class AsyncDelegateCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public AsyncDelegateCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public async void Execute(object? parameter)
    {
        try { await _execute(parameter); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"AsyncDelegateCommand error: {ex}"); }
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
