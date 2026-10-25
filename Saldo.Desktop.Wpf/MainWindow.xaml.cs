using System.Windows;
using System.ComponentModel;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IThemeService _themeService;
        private bool _waitingToClose;
        private bool _readyToClose;

        public MainWindow(MainViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _themeService = themeService;
            SourceInitialized += OnSourceInitialized;
            Loaded += (_, _) => viewModel.TransactionList.LoadCommand.Execute(null);
            Closing += OnClosing;
            Closed += (_, _) => (TryFindResource("Notifications") as NotificationsViewModel)?.Dispose();
        }

        private async void OnClosing(object? sender, CancelEventArgs args)
        {
            if (_readyToClose) return;
            var backups = TryFindResource("AutomaticBackup") as AutomaticBackupViewModel;
            if (!_viewModel.TransactionList.IsChangingPeriod && backups?.IsBusy != true) return;
            args.Cancel = true;
            if (_waitingToClose) return;
            _waitingToClose = true;
            IsEnabled = false;
            try
            {
                // A close still being committed may enqueue its backup later.
                await _viewModel.TransactionList.WaitForPeriodChangeAsync();
                if (backups is not null) await backups.WaitForIdleAsync();
                _readyToClose = true;
                Close();
            }
            finally
            {
                _waitingToClose = false;
                if (!_readyToClose) IsEnabled = true;
            }
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            ThemeService.ApplyWindowTitleBarTheme(this, _themeService.SelectedTheme);
        }
    }
}
