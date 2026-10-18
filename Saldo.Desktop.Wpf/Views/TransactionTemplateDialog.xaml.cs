using System.ComponentModel;
using System.Windows;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Views;

public partial class TransactionTemplateDialog : Window
{
    private bool _allowClose;
    private TransactionTemplateFormViewModel? _viewModel;
    public TransactionTemplateDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_viewModel is not null) _viewModel.RequestClose -= CloseAfterSave;
            _viewModel = e.NewValue as TransactionTemplateFormViewModel;
            if (_viewModel is not null) _viewModel.RequestClose += CloseAfterSave;
        };
        Closed += (_, _) => { if (_viewModel is not null) _viewModel.RequestClose -= CloseAfterSave; };
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current?.Resources["ThemeService"] is IThemeService theme)
                ThemeService.ApplyWindowTitleBarTheme(this, theme.SelectedTheme);
        };
    }
    private void CloseAfterSave(bool result) { _allowClose = true; DialogResult = result; }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.CanClose() != false) { _allowClose = true; DialogResult = false; }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && _viewModel?.CanClose() == false) e.Cancel = true;
    }
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        e.Handled = true;
        Cancel_Click(sender, e);
    }
}
