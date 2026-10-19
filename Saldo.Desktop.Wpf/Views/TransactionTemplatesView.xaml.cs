using System.Windows.Controls;

namespace Saldo.Desktop.Wpf.Views;

public partial class TransactionTemplatesView : UserControl
{
    private void ClearSearch_Click(object sender, System.Windows.RoutedEventArgs e) => TemplateSearchBox.Focus();
    public TransactionTemplatesView() => InitializeComponent();
}
