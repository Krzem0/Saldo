using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Saldo.Desktop.Wpf.Localization;

public sealed class CategoryColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string colorCode || string.IsNullOrWhiteSpace(colorCode))
        {
            return Brushes.Transparent;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorCode)!;
            return new SolidColorBrush(Color.FromArgb(82, color.R, color.G, color.B));
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
