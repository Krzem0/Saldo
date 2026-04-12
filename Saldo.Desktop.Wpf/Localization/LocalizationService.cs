using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Saldo.Desktop.Wpf.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager = new("Saldo.Desktop.Wpf.Localization.Strings", typeof(LocalizationService).Assembly);
    private readonly IReadOnlyList<CultureInfo> _availableCultures = [new CultureInfo("pl-PL"), new CultureInfo("en-US")];
    private CultureInfo _currentCulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CultureInfo> AvailableCultures => _availableCultures;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value))
            {
                return;
            }

            _currentCulture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.CurrentUICulture = value;
            CultureInfo.DefaultThreadCurrentCulture = value;
            CultureInfo.DefaultThreadCurrentUICulture = value;
            OnPropertyChanged(nameof(CurrentCulture));
            OnPropertyChanged(nameof(CultureInfo.CurrentUICulture));
            OnPropertyChanged("Item[]");
        }
    }

    public LocalizationService()
    {
        _currentCulture = _availableCultures[0];
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentCulture;
        CultureInfo.DefaultThreadCurrentCulture = _currentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;
    }

    public string this[string key] => _resourceManager.GetString(key, CurrentCulture) ?? key;

    public string Format(string key, params object[] args) => string.Format(CurrentCulture, this[key], args);

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
