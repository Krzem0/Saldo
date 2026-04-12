using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Desktop.Wpf.Localization;

public abstract class LocalizedViewModelBase : ViewModelBase
{
    protected ILocalizationService Localization { get; }

    protected LocalizedViewModelBase(ILocalizationService localization)
    {
        Localization = localization;
        Localization.PropertyChanged += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ILocalizationService.CurrentCulture) || e.PropertyName == "Item[]")
            {
                OnCultureChanged();
            }
        };
    }

    protected string T(string key) => Localization[key];

    protected virtual void OnCultureChanged() { }
}
