using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Saldo.Desktop.Wpf.Localization;

/// <summary>Uses an optional tag color, falling back to the theme brush supplied by the control.</summary>
public sealed class TagColorBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is string code && code.Length == 7
            && code[0] == '#' && code[1..].All(Uri.IsHexDigit))
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(code)!);
            brush.Freeze();
            return brush;
        }
        return values.Length > 1 && values[1] is Brush fallback ? fallback : Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(_ => Binding.DoNothing).ToArray();
}
