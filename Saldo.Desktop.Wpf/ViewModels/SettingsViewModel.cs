using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class SettingsViewModel : LocalizedViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IDatabaseFolderService _databaseFolderService;

    public SettingsViewModel(ILocalizationService localization, IThemeService themeService,
        IServiceScopeFactory scopeFactory, IDialogService dialogService, ILogger<SettingsViewModel> logger,
        IDatabaseFolderService databaseFolderService)
        : base(localization)
    {
        _themeService = themeService;
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        _logger = logger;
        _databaseFolderService = databaseFolderService;
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
        OpenDatabaseFolderCommand = new RelayCommand(OpenDatabaseFolder);
    }

    public ICommand CreateBackupCommand { get; }
    public ICommand OpenDatabaseFolderCommand { get; }

    private void OpenDatabaseFolder()
    {
        try
        {
            _databaseFolderService.OpenFolder();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open the database folder.");
            MessageBox.Show(T("OpenDatabaseFolderFailed"), T("BackupTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CreateBackupAsync()
    {
        var path = _dialogService.ShowBackupSaveDialog(T("BackupTitle"),
            $"saldo-backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.db", T("BackupFileFilter"));
        if (path is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>().CreateAsync(path);
            _logger.LogInformation("Database backup created at {BackupPath}.", path);
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, T("BackupSuccessMessage"), path),
                T("BackupTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database backup failed.");
            MessageBox.Show(T("BackupFailedMessage"), T("BackupTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public IReadOnlyList<CultureInfo> AvailableCultures => Localization.AvailableCultures;

    public CultureInfo CurrentCulture
    {
        get => Localization.CurrentCulture;
        set => Localization.CurrentCulture = value;
    }

    public IReadOnlyList<AppearanceThemeOption> ThemeOptions =>
    [
        new(AppearanceTheme.System, T("Theme_System")),
        new(AppearanceTheme.Light, T("Theme_Light")),
        new(AppearanceTheme.Dark, T("Theme_Dark"))
    ];

    public AppearanceTheme SelectedTheme
    {
        get => _themeService.SelectedTheme;
        set
        {
            _themeService.SelectedTheme = value;
            OnPropertyChanged(nameof(SelectedThemeOption));
        }
    }

    public AppearanceThemeOption? SelectedThemeOption
    {
        get => ThemeOptions.First(option => option.Theme == SelectedTheme);
        set
        {
            if (value is not null)
            {
                SelectedTheme = value.Theme;
            }
        }
    }

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged(nameof(ThemeOptions));
        OnPropertyChanged(nameof(SelectedThemeOption));
    }
}
