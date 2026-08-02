using System.Globalization;
using Saldo.Desktop.Wpf.Localization;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class SettingsViewModel : LocalizedViewModelBase
{
    public SettingsViewModel(ILocalizationService localization)
        : base(localization)
    {
    }

    public IReadOnlyList<CultureInfo> AvailableCultures => Localization.AvailableCultures;

    public CultureInfo CurrentCulture
    {
        get => Localization.CurrentCulture;
        set => Localization.CurrentCulture = value;
    }

    protected override void OnCultureChanged() => OnPropertyChanged(nameof(CurrentCulture));
}
