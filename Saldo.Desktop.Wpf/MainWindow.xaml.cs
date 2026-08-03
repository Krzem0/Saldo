using System.Windows;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IThemeService _themeService;

        public MainWindow(MainViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _themeService = themeService;
            SourceInitialized += OnSourceInitialized;
            Loaded += (_, _) => viewModel.TransactionList.LoadCommand.Execute(null);
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            ThemeService.ApplyWindowTitleBarTheme(this, _themeService.SelectedTheme);
        }
    }
}
