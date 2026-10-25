using System.Windows.Input;

namespace Saldo.Desktop.Wpf.Infrastructure;

public sealed class AsyncRelayCommand<T>(Func<T, Task> execute, Func<T, bool>? canExecute = null) : ICommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && parameter is T value && (canExecute?.Invoke(value) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter) || parameter is not T value) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try { await execute(value); }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
