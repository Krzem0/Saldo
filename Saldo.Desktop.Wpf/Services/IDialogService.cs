using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Application.Interfaces;

namespace Saldo.Desktop.Wpf.Services;

public interface IDialogService
{
    void Notify(string code, string message, string? details = null,
        NotificationSeverity severity = NotificationSeverity.Success) { }
    bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel);
    bool? ShowTransactionTemplate(TransactionTemplateFormViewModel viewModel) => throw new NotSupportedException();

    /// <summary>Shows a simple single-field name input dialog. Returns the entered name or null when cancelled.</summary>
    string? ShowNameDialog(string title, string? initialValue = null);

    ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null, bool allowIcons = false, string? initialIconKey = null);

    bool ConfirmDelete(string title, string message);
    bool ConfirmAccountingPeriodChange(string title, string message, string actionLabel) => throw new NotSupportedException();

    UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message);

    string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter);
    void ShowMessage(string title, string message) => throw new NotSupportedException();
    void ShowMessage(string title, string message, MessageDialogKind kind) => throw new NotSupportedException();
}
