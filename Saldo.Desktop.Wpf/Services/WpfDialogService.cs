using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Desktop.Wpf.Views;
using System.Windows;

namespace Saldo.Desktop.Wpf.Services;

public sealed class WpfDialogService : IDialogService
{
    public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel)
    {
        var dialog = new AddEditTransactionDialog { DataContext = viewModel };
        SetOwner(dialog);
        return dialog.ShowDialog();
    }

    public string? ShowNameDialog(string title, string? initialValue = null)
    {
        var dialog = new NameDialog(title, initialValue);
        SetOwner(dialog);
        return dialog.ShowDialog() == true ? dialog.EnteredName : null;
    }

    private static void SetOwner(Window dialog)
    {
        var owner = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;

        if (owner is not null)
        {
            dialog.Owner = owner;
        }
    }
}
