using System.Windows;

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
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnteredName)) return;
        DialogResult = true;
    }
}
