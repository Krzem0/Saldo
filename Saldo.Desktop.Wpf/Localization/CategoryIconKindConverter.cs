using System.Globalization;
using System.Windows.Data;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Localization;

public sealed class CategoryIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => CategoryIconCatalog.Resolve(value as string);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
