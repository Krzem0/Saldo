using System.Windows;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Views;

public partial class NameDialog : Window
{
    public string EnteredName { get; set; }

    public NameDialog(string title, string? initialValue)
    {
        InitializeComponent();
        Title = title;
        EnteredName = initialValue ?? string.Empty;
        DataContext = this;
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
        {
            ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnteredName))
        {
            NameErrorText.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
