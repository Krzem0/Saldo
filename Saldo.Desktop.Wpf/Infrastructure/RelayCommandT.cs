using System.Windows.Input;

namespace Saldo.Desktop.Wpf.Infrastructure;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        parameter is T t ? (_canExecute?.Invoke(t) ?? true) : true;

    public void Execute(object? parameter)
    {
        if (parameter is T t) _execute(t);
    }
}
