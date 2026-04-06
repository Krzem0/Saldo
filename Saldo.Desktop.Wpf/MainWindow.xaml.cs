using System.Windows;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += (_, _) => viewModel.TransactionList.LoadCommand.Execute(null);
        }
    }
}
