using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Services;

public interface IDialogService
{
    bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel);
}
