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

    public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null, bool allowIcons = false, string? initialIconKey = null)
    {
        var dialog = new ReferenceColorDialog(title, initialName, initialColorCode, allowIcons, initialIconKey);
        SetOwner(dialog);
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool ConfirmDelete(string title, string message)
    {
        var dialog = new DeleteConfirmationDialog(title, message);
        SetOwner(dialog);
        return dialog.ShowDialog() == true;
    }

    public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message)
    {
        var dialog = new UnsavedChangesDialog(title, message);
        SetOwner(dialog);
        dialog.ShowDialog();
        return dialog.Choice;
    }

    public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            FileName = suggestedFileName,
            DefaultExt = ".db",
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true,
            CheckPathExists = true,
            InitialDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Saldo")
        };
        var owner = GetActiveOwner();
        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return accepted == true ? dialog.FileName : null;
    }

    private static Window? GetActiveOwner()
        => System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;

    private static void SetOwner(Window dialog)
    {
        var owner = GetActiveOwner();

        if (owner is not null)
        {
            dialog.Owner = owner;
        }
    }
}
