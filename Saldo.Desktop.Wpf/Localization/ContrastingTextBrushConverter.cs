using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Saldo.Desktop.Wpf.Localization;

/// <summary>Chooses black or white text with the greater contrast against an opaque chip fill.</summary>
public sealed class ContrastingTextBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SolidColorBrush brush) return Brushes.Black;
        static double Linear(byte channel)
        {
            var component = channel / 255d;
            return component <= 0.04045 ? component / 12.92 : Math.Pow((component + 0.055) / 1.055, 2.4);
        }
        var color = brush.Color;
        var luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        return (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05) ? Brushes.Black : Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
