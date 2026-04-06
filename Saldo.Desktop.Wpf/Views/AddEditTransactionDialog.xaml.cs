using System.Windows;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Views;

public partial class AddEditTransactionDialog : Window
{
    public AddEditTransactionDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AddEditTransactionViewModel vm)
            vm.RequestClose += result => { DialogResult = result; };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
