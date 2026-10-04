using System.ComponentModel;
using System.Windows;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Views;

public partial class AddEditTransactionDialog : Window
{
    private bool _allowClose;

    public AddEditTransactionDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
        {
            ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AddEditTransactionViewModel vm)
            vm.RequestClose += result =>
            {
                _allowClose = true;
                DialogResult = result;
            };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (CanClose())
        {
            _allowClose = true;
            DialogResult = false;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (!CanClose())
        {
            e.Cancel = true;
        }
    }

    private bool CanClose()
        => DataContext is not AddEditTransactionViewModel vm || vm.CanClose();
}
