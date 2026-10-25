using System.Windows;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Views;

public partial class NotificationHistoryDialog : Window
{
    public NotificationHistoryDialog()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            if (TryFindResource("ThemeService") is IThemeService theme)
                ThemeService.ApplyWindowTitleBarTheme(this, theme.SelectedTheme);
        };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
