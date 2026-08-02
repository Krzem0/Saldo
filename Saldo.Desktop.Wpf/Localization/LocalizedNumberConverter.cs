using System.Globalization;
using System.Windows.Data;

namespace Saldo.Desktop.Wpf.Localization;

public sealed class LocalizedNumberConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not IFormattable number)
        {
            return string.Empty;
        }

        var selectedCulture = values.Length > 1 && values[1] is CultureInfo currentCulture
            ? currentCulture
            : CultureInfo.CurrentCulture;

        return number.ToString("N2", selectedCulture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
