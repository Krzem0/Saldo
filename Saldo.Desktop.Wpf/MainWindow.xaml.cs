using System.Windows;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            Loaded += (_, _) => viewModel.TransactionList.LoadCommand.Execute(null);
        }
    }
}
