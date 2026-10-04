using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Services;

public interface IDialogService
{
    bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel);

    /// <summary>Shows a simple single-field name input dialog. Returns the entered name or null when cancelled.</summary>
    string? ShowNameDialog(string title, string? initialValue = null);

    CategoryDialogResult? ShowCategoryDialog(string title, string? initialName = null, string? initialColorCode = null);

    bool ConfirmDelete(string title, string message);
}
