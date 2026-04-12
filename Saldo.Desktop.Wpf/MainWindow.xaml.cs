using System.Windows;
using System.Windows.Controls;
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

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl tc) return;
            if (tc.SelectedIndex == 1) _viewModel.Categories.LoadCommand.Execute(null);
            else if (tc.SelectedIndex == 2) _viewModel.Parties.LoadCommand.Execute(null);
        }
    }
}
