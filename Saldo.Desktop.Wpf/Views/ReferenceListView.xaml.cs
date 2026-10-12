using System.Windows.Controls;

namespace Saldo.Desktop.Wpf.Views;

public partial class ReferenceListView : UserControl
{
    private void ClearSearch_Click(object sender, System.Windows.RoutedEventArgs e) => ReferenceSearchBox.Focus();
    public ReferenceListView()
    {
        InitializeComponent();
    }
}
