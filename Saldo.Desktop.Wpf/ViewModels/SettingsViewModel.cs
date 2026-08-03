using System.Globalization;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class SettingsViewModel : LocalizedViewModelBase
{
    private readonly IThemeService _themeService;

    public SettingsViewModel(ILocalizationService localization, IThemeService themeService)
        : base(localization)
    {
        _themeService = themeService;
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
