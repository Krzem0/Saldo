using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Desktop.Wpf.Views;

namespace Saldo.Desktop.Wpf.Services;

public sealed class WpfDialogService : IDialogService
{
    public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel)
    {
        var dialog = new AddEditTransactionDialog { DataContext = viewModel };
        return dialog.ShowDialog();
    }

    public string? ShowNameDialog(string title, string? initialValue = null)
    {
        var dialog = new NameDialog(title, initialValue);
        return dialog.ShowDialog() == true ? dialog.EnteredName : null;
    }
}
