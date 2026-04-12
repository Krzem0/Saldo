using System.Globalization;
using System.Windows.Data;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.Localization;

public sealed class TransactionDirectionToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TransactionDirection direction)
        {
            return string.Empty;
        }

        var localization = System.Windows.Application.Current?.Resources["Localization"] as ILocalizationService;
        if (localization is null)
        {
            return direction.ToString();
        }

        return direction switch
        {
            TransactionDirection.Expense => localization["Direction_Expense"],
            TransactionDirection.Income => localization["Direction_Income"],
            _ => direction.ToString()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
