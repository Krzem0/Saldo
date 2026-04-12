using System.ComponentModel;
using System.Globalization;

namespace Saldo.Desktop.Wpf.Localization;

public interface ILocalizationService : INotifyPropertyChanged
{
    IReadOnlyList<CultureInfo> AvailableCultures { get; }

    CultureInfo CurrentCulture { get; set; }

    string this[string key] { get; }

    string Format(string key, params object[] args);
}
