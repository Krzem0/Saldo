using System.Windows;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Views;

public partial class DeleteConfirmationDialog : Window
{
    public string Message { get; }

    public DeleteConfirmationDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        Message = message;
        DataContext = this;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
        {
            ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
