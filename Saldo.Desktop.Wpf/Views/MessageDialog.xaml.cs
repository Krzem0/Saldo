using System.Windows;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Views;

public partial class MessageDialog : Window
{
    public string Message { get; }
    public MessageDialogKind? Kind { get; }

    public MessageDialog(string title, string message, MessageDialogKind? kind = null)
    {
        InitializeComponent();
        Title = title;
        Message = message;
        Kind = kind;
        DataContext = this;
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
                ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        };
    }
}
