using System.Windows;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Views;

public partial class AccountingPeriodConfirmationDialog : Window
{
    public string Message { get; }
    public string ActionLabel { get; }

    public AccountingPeriodConfirmationDialog(string title, string message, string actionLabel)
    {
        InitializeComponent();
        Title = title;
        Message = message;
        ActionLabel = actionLabel;
        DataContext = this;
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
                ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
