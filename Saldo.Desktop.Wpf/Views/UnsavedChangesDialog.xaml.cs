using System.Windows;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Views;

public partial class UnsavedChangesDialog : Window
{
    public string Message { get; }
    public UnsavedChangesChoice Choice { get; private set; } = UnsavedChangesChoice.Cancel;

    public UnsavedChangesDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        Message = message;
        DataContext = this;
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
                ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Save;
        DialogResult = true;
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Discard;
        DialogResult = true;
    }
}
